using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Storages.Encryption;
using PlikShare.Users.Cache;
using PlikShare.Users.Entities;
using PlikShare.Users.GetOrCreate;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Members.GrantEncryptionAccess;
using PlikShare.Workspaces.Permissions;
using Serilog;

namespace PlikShare.Workspaces.Members.CreateInvitation;

public class CreateWorkspaceMemberInvitationOperation(
    DbWriteQueue dbWriteQueue,
    CreateWorkspaceMemberInvitationQuery createWorkspaceMemberInvitationQuery,
    GetOrCreateUserInvitationQuery getOrCreateUserInvitationQuery,
    GrantEncryptionAccessOperation grantEncryptionAccessOperation)
{
    /// <summary>
    /// For a full-encryption workspace, invitees who already have encryption configured at
    /// invite time are auto-granted a wek wrap immediately — the owner is unlocked (enforced
    /// by the endpoint's session filter) and the invitee's public key is known, so there is
    /// nothing to defer. Invitees without encryption follow the deferred flow: no wek at
    /// invite; when they later set up a password, <c>NotifyOwnersOfPendingGrantsQuery</c>
    /// notifies the owner to grant manually.
    ///
    /// Every DB write the invite path produces — user invitation row (for brand-new emails),
    /// workspace membership insert, invitation email enqueue, and (for auto-grant candidates)
    /// the wek upserts — runs in a single SQLite transaction. Anything that fails rolls back
    /// the whole batch; nothing else can resurrect a partial state, because the deferred
    /// owner-notification only fires at invitee password setup (which an invitee with existing
    /// encryption has already done) and an orphan invitation user row would route the next
    /// retry through the "already exists" branch with no fresh invitation code to email.
    /// </summary>
    public async Task<Result> Execute(
        WorkspaceContext workspace,
        UserContext inviter,
        IEnumerable<Email> memberEmails,
        WorkspacePermissions permission,
        WorkspaceEncryptionSession? ownerSession,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var emails = memberEmails.ToArray();

        var members = await dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace: workspace,
                inviter: inviter,
                memberEmails: emails,
                ownerSession: ownerSession,
                allowShare: permission.AllowShare,
                correlationId: correlationId),
            cancellationToken: cancellationToken);

        return new Result(
            Members: members);
    }

    private UserContext[] ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        WorkspaceContext workspace,
        UserContext inviter,
        Email[] memberEmails,
        WorkspaceEncryptionSession? ownerSession,
        bool allowShare,
        Guid correlationId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var members = new List<CreateWorkspaceMemberInvitationQuery.Member>();

            foreach (var email in memberEmails)
            {
                var resolved = getOrCreateUserInvitationQuery.ExecuteTransaction(
                    dbWriteContext: dbWriteContext,
                    transaction: transaction,
                    email: email);

                members.Add(new CreateWorkspaceMemberInvitationQuery.Member(
                    User: resolved.User,
                    InvitationCode: resolved.InvitationCode));
            }

            var autoGrants = BuildAutoGrants(
                workspace, 
                ownerSession, 
                members);

            var insertedMemberIds = createWorkspaceMemberInvitationQuery.ExecuteTransaction(
                dbWriteContext: dbWriteContext,
                transaction: transaction,
                workspace: workspace,
                inviter: inviter,
                members: members,
                allowShare: allowShare,
                correlationId: correlationId);

            var insertedSet = insertedMemberIds.ToHashSet();
            var autoGrantedCount = 0;

            foreach (var grant in autoGrants)
            {
                if (!insertedSet.Contains(grant.Target.Id))
                    continue;

                grantEncryptionAccessOperation.ExecuteTransaction(
                    dbWriteContext: dbWriteContext,
                    transaction: transaction,
                    workspace: workspace,
                    owner: inviter,
                    target: grant.Target,
                    wrapped: grant.Wrapped,
                    correlationId: correlationId,
                    notifyTarget: false);

                autoGrantedCount++;
            }

            transaction.Commit();

            Log.Information(
                "Workspace#{WorkspaceId} invitation by Inviter '{InviterId}' completed. " +
                "Inserted {InsertedCount} of {RequestedCount} membership(s); " +
                "auto-granted encryption access to {AutoGrantedCount} invitee(s).",
                workspace.Id,
                inviter.Id,
                insertedMemberIds.Length,
                members.Count,
                autoGrantedCount);

            return members.Select(m => m.User).ToArray();
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e,
                "Failed to create Workspace#{WorkspaceId} invitation by Inviter '{InviterId}' " +
                "for emails '{MemberEmails}'. Whole transaction rolled back.",
                workspace.Id,
                inviter.Id,
                memberEmails.Select(em => em.Anonymize()));

            throw;
        }
    }

    private static AutoGrant[] BuildAutoGrants(
        WorkspaceContext workspace,
        WorkspaceEncryptionSession? ownerSession,
        List<CreateWorkspaceMemberInvitationQuery.Member> members)
    {
        if (workspace.Storage.Encryption.Type != StorageEncryptionType.Full)
            return [];

        if (ownerSession is null)
            return [];

        return members
            .Where(m => m.User.EncryptionMetadata is not null)
            .Select(m => new AutoGrant
            {
                Target = m.User,
                Wrapped = GrantEncryptionAccessOperation.BuildWrapped(ownerSession, m.User)
            })
            .ToArray();
    }

    private class AutoGrant
    {
        public required UserContext Target { get; init; }
        public required GrantEncryptionAccessOperation.WrappedVersion[] Wrapped { get; init; }
    }

    public readonly record struct Result(
        UserContext[]? Members = default);
}
