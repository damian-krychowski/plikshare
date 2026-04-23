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
using Microsoft.Data.Sqlite;

namespace PlikShare.Workspaces.Members.CreateInvitation;

public class CreateWorkspaceMemberInvitationOperation(
    DbWriteQueue dbWriteQueue,
    IClock clock,
    IQueue queue,
    CreateWorkspaceMemberInvitationQuery createWorkspaceMemberInvitationQuery,
    GetOrCreateUserInvitationQuery getOrCreateUserInvitationQuery,
    GrantEncryptionAccessOperation grantEncryptionAccessOperation,
    CreateOrGetEphemeralUserKeyPairQuery createOrGetEphemeralUserKeyPairQuery,
    UpsertEphemeralWorkspaceEncryptionKeyQuery upsertEphemeralWorkspaceEncryptionKeyQuery)
{
    /// <summary>
    /// For a full-encryption workspace, three invitee profiles get different treatment:
    ///
    /// 1. Existing user with encryption already set up → immediate auto-grant of wek wraps
    ///    (owner's unlocked session is enforced by the endpoint's session filter).
    /// 2. Brand-new invitee (fresh user row + invitation code) → an ephemeral user keypair
    ///    is created (or reused if a prior invitation already staged one), its private key
    ///    wrapped with a KEK derived from the invitation code; the workspace DEK is then
    ///    sealed to the ephemeral public key and persisted as an ewek row. A cleanup queue
    ///    job wipes the ewek row after the owner-chosen TTL. The invitee promotes the wrap
    ///    during encryption-password setup using the invitation code they received by email.
    /// 3. Existing user without encryption → deferred: no wek and no ewek at invite time.
    ///    When they later set up encryption, <c>NotifyOwnersOfPendingGrantsQuery</c> notifies
    ///    the owner to grant manually.
    ///
    /// Every DB write the invite path produces — user invitation row (for brand-new emails),
    /// ephemeral keypair creation, workspace membership insert, invitation email enqueue, wek
    /// upserts for auto-grant, ewek upserts for the ephemeral path, and the cleanup queue job
    /// enqueue — runs in a single SQLite transaction. Anything that fails rolls back the whole
    /// batch.
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

            var insertResult = createWorkspaceMemberInvitationQuery.ExecuteTransaction(
                dbWriteContext: dbWriteContext,
                transaction: transaction,
                workspace: workspace,
                inviter: inviter,
                members: members,
                allowShare: allowShare,
                correlationId: correlationId);
            
            var shouldGrantFullEncryptionAccess =
                workspace.Storage.Encryption.Type == StorageEncryptionType.Full
                && ownerSession is not null;

            if (shouldGrantFullEncryptionAccess)
            {
                var newlyInvitedMembers = members
                    .Where(member => insertResult.NewlyInvitedMemberIds.Contains(
                        member.User.Id))
                    .ToList();

                var (autoGrantedCount, ephemeralGrantedCount) = GrantAccessToFullyEncryptedWorkspace(
                    dbWriteContext: dbWriteContext,
                    workspace: workspace,
                    inviter: inviter,
                    ownerSession: ownerSession!,
                    ephemeralDekLifetime: ephemeralDekLifetime,
                    correlationId: correlationId,
                    newlyInvitedMembers: newlyInvitedMembers,
                    transaction: transaction);
                
                Log.Information(
                    "Workspace#{WorkspaceId} invitation by Inviter '{InviterId}' completed. " +
                    "Inserted {InsertedCount} of {RequestedCount} membership(s); " +
                    "auto-granted encryption access to {AutoGrantedCount} invitee(s); " +
                    "staged {EphemeralGrantedCount} ephemeral DEK(s).",
                    workspace.Id,
                    inviter.Id,
                    insertResult.NewlyInvitedMemberIds.Count,
                    members.Count,
                    autoGrantedCount,
                    ephemeralGrantedCount);
            }
            else
            {
                Log.Information(
                    "Workspace#{WorkspaceId} invitation by Inviter '{InviterId}' completed. " +
                    "Inserted {InsertedCount} of {RequestedCount} membership(s)",
                    workspace.Id,
                    inviter.Id,
                    insertResult.NewlyInvitedMemberIds.Count,
                    members.Count);
            }
            
            transaction.Commit();

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

    private GrantedAccessCounter GrantAccessToFullyEncryptedWorkspace(
        SqliteWriteContext dbWriteContext, 
        WorkspaceContext workspace,
        UserContext inviter, 
        WorkspaceEncryptionSession ownerSession, 
        TimeSpan? ephemeralDekLifetime,
        Guid correlationId,
        List<CreateWorkspaceMemberInvitationQuery.Member> newlyInvitedMembers, 
        SqliteTransaction transaction)
    {
        var autoGrants = BuildAutoGrants(
            ownerSession: ownerSession,
            members: newlyInvitedMembers);
            
        var autoGrantedCount = 0;
        var ephemeralGrantedCount = 0;

        foreach (var grant in autoGrants)
        {
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
            
        var ephemeralGrants = BuildEphemeralGrants(
            dbWriteContext: dbWriteContext,
            transaction: transaction,
            members: newlyInvitedMembers);

        if (ephemeralGrants.Length > 0)
        {
            // Hard precondition on the full eligible set: every row we're about to stage needs
            // a cleanup job with a TTL, including the subsequent-invite case that reuses an
            // existing euek. Silently continuing would produce ewek rows with no scheduled
            // expiry — live credentials with no bound.
            if (ephemeralDekLifetime is null)
                throw new InvalidOperationException(
                    $"Ephemeral DEK lifetime is required when inviting {ephemeralGrants.Length} user(s) " +
                    $"eligible for ephemeral staging on full-encryption Workspace#{workspace.Id}. " +
                    $"The caller must validate this precondition.");

            var expiresAt = clock.UtcNow + ephemeralDekLifetime.Value;

            foreach (var grant in ephemeralGrants)
            {
                var wasStaged = StageEphemeralGrant(
                    dbWriteContext: dbWriteContext,
                    transaction: transaction,
                    workspace: workspace,
                    inviter: inviter,
                    ownerSession: ownerSession!,
                    grant: grant,
                    expiresAt: expiresAt,
                    correlationId: correlationId);

                if (wasStaged)
                    ephemeralGrantedCount++;
            }
        }

        return new GrantedAccessCounter(
            AutoGrantedCount: autoGrantedCount,
            EphemeralGrantedCount: ephemeralGrantedCount);
    }

    private bool StageEphemeralGrant(
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction,
        WorkspaceContext workspace,
        UserContext inviter,
        WorkspaceEncryptionSession ownerSession,
        EphemeralGrant grant,
        DateTimeOffset expiresAt,
        Guid correlationId)
    {
        var invitationCodeBytes = grant.InvitationCode is null
            ? null
            : Base62Encoding.FromBase62ToBytes(grant.InvitationCode.Value);

        try
        {
            var ephemeralPublicKey = createOrGetEphemeralUserKeyPairQuery.ExecuteTransaction(
                dbWriteContext: dbWriteContext,
                userId: grant.Target.Id,
                invitationCodeBytes: invitationCodeBytes,
                createdByUserId: inviter.Id,
                transaction: transaction);

            if (ephemeralPublicKey is null)
            {
                Log.Warning(
                    "Workspace#{WorkspaceId} ephemeral DEK staging skipped for User#{UserId}: " +
                    "no existing ephemeral keypair and no invitation code available to create one. " +
                    "User falls back to the deferred-grant path on encryption-password setup.",
                    workspace.Id, grant.Target.Id);

                return false;
            }

            foreach (var entry in ownerSession.Entries)
            {
                var sealedDek = entry.Dek.Use(
                    state: ephemeralPublicKey.Value,
                    action: static (dekSpan, pubKey) => UserKeyPair.SealTo(pubKey.Bytes, dekSpan));

                upsertEphemeralWorkspaceEncryptionKeyQuery.ExecuteTransaction(
                    dbWriteContext: dbWriteContext,
                    workspaceId: workspace.Id,
                    userId: grant.Target.Id,
                    storageDekVersion: entry.StorageDekVersion,
                    encryptedWorkspaceDek: sealedDek,
                    expiresAt: expiresAt,
                    createdByUserId: inviter.Id,
                    transaction: transaction);
            }
        }
        finally
        {
            if (invitationCodeBytes is not null)
                CryptographicOperations.ZeroMemory(invitationCodeBytes);
        }

        queue.Enqueue(
            correlationId: correlationId,
            jobType: DeleteEphemeralWorkspaceEncryptionKeysQueueJobType.Value,
            definition: new DeleteEphemeralWorkspaceEncryptionKeysQueueJobDefinition
            {
                WorkspaceId = workspace.Id,
                UserId = grant.Target.Id
            },
            executeAfterDate: expiresAt,
            debounceId: $"ewek-cleanup-{workspace.Id}-{grant.Target.Id}",
            sagaId: null,
            dbWriteContext: dbWriteContext,
            transaction: transaction);

        return true;
    }

    private static AutoGrant[] BuildAutoGrants(
        WorkspaceEncryptionSession ownerSession,
        IEnumerable<CreateWorkspaceMemberInvitationQuery.Member> members)
    {
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
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction,
        IEnumerable<CreateWorkspaceMemberInvitationQuery.Member> members)
    {
        var candidates = members
            .Where(m => m.User.EncryptionMetadata is null)
            .ToArray();

        if (candidates.Length == 0)
            return [];

        // An invitee without encryption is eligible for the ephemeral path either because
        // they are brand-new (fresh u_users row → InvitationCode just issued → we can wrap
        // the ephemeral private key with it) OR because a prior invite already staged an
        // ephemeral keypair for them (subsequent invite from a different workspace → we
        // reuse the shared euek_public_key without touching the code).
        //
        // Candidates that are neither — registered-without-encryption + no prior euek —
        // fall into the deferred grant path: membership row + NotifyOwnersOfPendingGrantsQuery
        // after the invitee sets up encryption. No ewek is staged for them here, so they
        // are intentionally excluded from the TTL precondition below.
        var existingEuekUserIds = CandidatesWithExistingEuek(
            dbWriteContext,
            transaction,
            candidates);

        var eligible = candidates
            .Where(candidate => candidate.InvitationCode is not null || existingEuekUserIds.Contains(candidate.User.Id))
            .ToArray();

        if (eligible.Length == 0)
            return [];
        
        return eligible
            .Select(m => new EphemeralGrant
            {
                Target = m.User,
                InvitationCode = m.InvitationCode
            })
            .ToArray();
    }


    private static HashSet<int> CandidatesWithExistingEuek(
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction, 
        CreateWorkspaceMemberInvitationQuery.Member[] candidates)
    {
        var candidatesWithoutCode = candidates
            .Where(m => m.InvitationCode is null)
            .Select(m => m.User.Id)
            .ToArray();

        if (candidatesWithoutCode.Length == 0)
            return [];

        return dbWriteContext
            .Cmd(
                sql: """
                     SELECT euek_user_id
                     FROM euek_ephemeral_user_encryption_keys
                     WHERE euek_user_id IN (
                        SELECT value FROM json_each($userIds)
                     )
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithJsonParameter("$userIds", candidatesWithoutCode)
            .Execute()
            .ToHashSet();
    }

    private readonly record struct GrantedAccessCounter(
        int AutoGrantedCount,
        int EphemeralGrantedCount);

    private class AutoGrant
    {
        public required UserContext Target { get; init; }
        public required GrantEncryptionAccessOperation.WrappedVersion[] Wrapped { get; init; }
    }

    private class EphemeralGrant
    {
        public required UserContext Target { get; init; }
        public required InvitationCode? InvitationCode { get; init; }
    }

    public readonly record struct Result(
        UserContext[]? Members = default);
}
