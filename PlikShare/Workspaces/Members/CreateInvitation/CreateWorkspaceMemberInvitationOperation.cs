using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Storages.Encryption;
using PlikShare.Users.Cache;
using PlikShare.Users.Entities;
using PlikShare.Users.GetOrCreate;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Members.GrantEncryptionAccess;
using PlikShare.Workspaces.Members.GrantEncryptionAccess.Cleanup;
using PlikShare.Workspaces.Permissions;
using Serilog;
using System.Security.Cryptography;

namespace PlikShare.Workspaces.Members.CreateInvitation;

public class CreateWorkspaceMemberInvitationOperation(
    DbWriteQueue dbWriteQueue,
    IClock clock,
    IQueue queue,
    CreateWorkspaceMemberInvitationQuery createWorkspaceMemberInvitationQuery,
    GetOrCreateUserInvitationQuery getOrCreateUserInvitationQuery,
    GrantEncryptionAccessOperation grantEncryptionAccessOperation,
    UpsertEphemeralWorkspaceEncryptionKeyQuery upsertEphemeralWorkspaceEncryptionKeyQuery)
{
    /// <summary>
    /// For a full-encryption workspace, three invitee profiles get different treatment:
    ///
    /// 1. Existing user with encryption already set up → immediate auto-grant of wek wraps
    ///    (owner's unlocked session is enforced by the endpoint's session filter).
    /// 2. Brand-new invitee (fresh user row + invitation code) → ephemeral wrap staged under
    ///    a KEK derived from the invitation code, paired with a cleanup queue job that wipes
    ///    the ewek rows after the owner-chosen TTL. The invitee promotes the wrap to wek
    ///    during their encryption-password setup using the invitation code they received in
    ///    the invitation email.
    /// 3. Existing user without encryption → deferred: no wek and no ewek at invite time.
    ///    When they later set up encryption, <c>NotifyOwnersOfPendingGrantsQuery</c> notifies
    ///    the owner to grant manually.
    ///
    /// Every DB write the invite path produces — user invitation row (for brand-new emails),
    /// workspace membership insert, invitation email enqueue, wek upserts for auto-grant,
    /// ewek upserts for the ephemeral path, and the cleanup queue job enqueue — runs in a
    /// single SQLite transaction. Anything that fails rolls back the whole batch.
    /// </summary>
    public static readonly TimeSpan MaxEphemeralDekLifetime = TimeSpan.FromDays(30);

    public async Task<Result> Execute(
        WorkspaceContext workspace,
        UserContext inviter,
        IEnumerable<Email> memberEmails,
        WorkspacePermissions permission,
        WorkspaceEncryptionSession? ownerSession,
        TimeSpan? ephemeralDekLifetime,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        if (ephemeralDekLifetime is not null
            && (ephemeralDekLifetime.Value <= TimeSpan.Zero
                || ephemeralDekLifetime.Value > MaxEphemeralDekLifetime))
        {
            throw new ArgumentOutOfRangeException(
                nameof(ephemeralDekLifetime),
                ephemeralDekLifetime,
                $"Ephemeral DEK lifetime must be in (0, {MaxEphemeralDekLifetime.TotalDays} days].");
        }

        var emails = memberEmails.ToArray();

        var members = await dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace: workspace,
                inviter: inviter,
                memberEmails: emails,
                ownerSession: ownerSession,
                ephemeralDekLifetime: ephemeralDekLifetime,
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
        TimeSpan? ephemeralDekLifetime,
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

            var ephemeralGrants = BuildEphemeralGrants(
                workspace,
                ownerSession,
                members,
                ephemeralDekLifetime);

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
            var ephemeralGrantedCount = 0;

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

            var expiresAt = ephemeralDekLifetime is not null
                ? clock.UtcNow + ephemeralDekLifetime.Value
                : (DateTimeOffset?)null;

            foreach (var grant in ephemeralGrants)
            {
                if (!insertedSet.Contains(grant.Target.Id))
                    continue;

                foreach (var wrap in grant.Wrapped)
                {
                    upsertEphemeralWorkspaceEncryptionKeyQuery.ExecuteTransaction(
                        dbWriteContext: dbWriteContext,
                        workspaceId: workspace.Id,
                        userId: grant.Target.Id,
                        storageDekVersion: wrap.StorageDekVersion,
                        encryptedWorkspaceDek: wrap.EncryptedDek,
                        expiresAt: expiresAt!.Value,
                        createdByUserId: inviter.Id,
                        transaction: transaction);
                }

                queue.Enqueue(
                    correlationId: correlationId,
                    jobType: DeleteEphemeralWorkspaceEncryptionKeysQueueJobType.Value,
                    definition: new DeleteEphemeralWorkspaceEncryptionKeysQueueJobDefinition
                    {
                        WorkspaceId = workspace.Id,
                        UserId = grant.Target.Id
                    },
                    executeAfterDate: expiresAt!.Value,
                    debounceId: $"ewek-cleanup-{workspace.Id}-{grant.Target.Id}",
                    sagaId: null,
                    dbWriteContext: dbWriteContext,
                    transaction: transaction);

                ephemeralGrantedCount++;
            }

            transaction.Commit();

            Log.Information(
                "Workspace#{WorkspaceId} invitation by Inviter '{InviterId}' completed. " +
                "Inserted {InsertedCount} of {RequestedCount} membership(s); " +
                "auto-granted encryption access to {AutoGrantedCount} invitee(s); " +
                "staged {EphemeralGrantedCount} ephemeral DEK(s).",
                workspace.Id,
                inviter.Id,
                insertedMemberIds.Length,
                members.Count,
                autoGrantedCount,
                ephemeralGrantedCount);

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

    private static EphemeralGrant[] BuildEphemeralGrants(
        WorkspaceContext workspace,
        WorkspaceEncryptionSession? ownerSession,
        List<CreateWorkspaceMemberInvitationQuery.Member> members,
        TimeSpan? ephemeralDekLifetime)
    {
        if (workspace.Storage.Encryption.Type != StorageEncryptionType.Full)
            return [];

        if (ownerSession is null)
            return [];

        var eligible = members
            .Where(m => m.User.EncryptionMetadata is null && m.InvitationCode is not null)
            .ToArray();

        if (eligible.Length == 0)
            return [];

        // Hard precondition: the endpoint must validate and forward a lifetime whenever any
        // invitee qualifies for an ephemeral grant. Silently falling back to the deferred
        // flow here would strand the invitation code the user just received: they would
        // never be able to promote their ephemeral DEK (none exists), and the owner would
        // not be notified until the invitee sets up encryption. Failing loud forces callers
        // to pass the TTL.
        if (ephemeralDekLifetime is null)
            throw new InvalidOperationException(
                $"Ephemeral DEK lifetime is required when inviting {eligible.Length} brand-new user(s) " +
                $"to full-encryption Workspace#{workspace.Id}. The caller must validate this precondition.");

        return eligible
            .Select(m => new EphemeralGrant
            {
                Target = m.User,
                Wrapped = BuildEphemeralWrapped(ownerSession, m.InvitationCode!)
            })
            .ToArray();
    }

    private static EphemeralWrappedVersion[] BuildEphemeralWrapped(
        WorkspaceEncryptionSession ownerSession,
        InvitationCode invitationCode)
    {
        var invitationCodeBytes = Base62Encoding.FromBase62ToBytes(invitationCode.Value);

        try
        {
            return ownerSession.Entries
                .Select(entry => new EphemeralWrappedVersion(
                    StorageDekVersion: entry.StorageDekVersion,
                    EncryptedDek: entry.Dek.Use(
                        state: invitationCodeBytes,
                        (dekSpan, ikm) => InvitationCodeDekWrap.Wrap(ikm, dekSpan))))
                .ToArray();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(invitationCodeBytes);
        }
    }

    private class AutoGrant
    {
        public required UserContext Target { get; init; }
        public required GrantEncryptionAccessOperation.WrappedVersion[] Wrapped { get; init; }
    }

    private class EphemeralGrant
    {
        public required UserContext Target { get; init; }
        public required EphemeralWrappedVersion[] Wrapped { get; init; }
    }

    private readonly record struct EphemeralWrappedVersion(
        int StorageDekVersion,
        byte[] EncryptedDek);

    public readonly record struct Result(
        UserContext[]? Members = default);
}
