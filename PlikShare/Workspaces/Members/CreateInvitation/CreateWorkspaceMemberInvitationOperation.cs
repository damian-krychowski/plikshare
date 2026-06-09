using PlikShare.Core.Clock;
using PlikShare.Core.Configuration;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Emails;
using PlikShare.Core.Emails.Templates;
using PlikShare.Core.Encryption;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.GeneralSettings;
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
    IConfig config,
    IQueue queue,
    AppSettings appSettings,
    EmailProviderStore emailProviderStore,
    GenericEmailTemplate genericEmailTemplate,
    CreateWorkspaceMemberInvitationQuery createWorkspaceMemberInvitationQuery,
    GetOrCreateUserInvitationQuery getOrCreateUserInvitationQuery,
    GrantEncryptionAccessOperation grantEncryptionAccessOperation,
    CreateOrGetEphemeralUserKeyPairQuery createOrGetEphemeralUserKeyPairQuery,
    UpsertEphemeralWorkspaceEncryptionKeyQuery upsertEphemeralWorkspaceEncryptionKeyQuery,
    RollbackEncryptedInvitationQuery rollbackEncryptedInvitationQuery)
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
    /// Email delivery split: for non-full-encryption workspaces the invitation email is
    /// enqueued in the same transaction as the membership writes (legacy async path). For
    /// full-encryption workspaces the email is sent SYNCHRONOUSLY after commit and the
    /// whole DB state is rolled back if the send fails — the invitation code doubles as
    /// the KEK that wraps the ephemeral private key, so persisting the plaintext anywhere
    /// (queue payload, completed-jobs history) is a credential leak.
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

        var isFullEncryption = workspace.EncryptionType == StorageEncryptionType.Full;

        // Pre-flight: a full-encryption invite cannot proceed without an active provider, because
        // synchronous send is the only path that does not leak the plaintext invitation code.
        if (isFullEncryption && !emailProviderStore.IsEmailSenderAvailable)
            return new Result(ResultCode.EmailProviderNotConfigured, Members: []);

        var emails = memberEmails.ToArray();

        var staged = await dbWriteQueue.Execute(
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

        if (!isFullEncryption || staged.PendingSyncEmails.Count == 0)
        {
            // Non-FE workspace, or FE workspace where every invitee was a duplicate (no fresh
            // memberships → no emails to send). Nothing more to do.
            return new Result(ResultCode.Ok, staged.Members);
        }

        var successfullySentUserIds = await TrySendInvitationEmails(
            inviter: inviter,
            workspace: workspace,
            pendingEmails: staged.PendingSyncEmails,
            cancellationToken: cancellationToken);

        if (successfullySentUserIds.Count == staged.PendingSyncEmails.Count)
            return new Result(ResultCode.Ok, staged.Members);

        // Compensating delete excluding invitees whose emails actually went out — their
        // invitation stands and must not be unwound. CancellationToken.None on purpose:
        // rollback must run even if the inviter's request was cancelled, otherwise we leak
        // DB state.
        try
        {
            await rollbackEncryptedInvitationQuery.Execute(
                artifacts: staged.RollbackTracker.BuildRollbackArtifacts(
                    userIdsToExclude: successfullySentUserIds),
                cancellationToken: CancellationToken.None);
        }
        catch (Exception rollbackException)
        {
            Log.Error(rollbackException,
                "Compensating rollback for Workspace#{WorkspaceId} invitation failed AFTER a " +
                "send failure. DB state is inconsistent — manual cleanup required.",
                workspace.Id);
        }

        return new Result(ResultCode.EmailSendFailed, Members: []);
    }

    /// <summary>
    /// Synchronous per-invitee send used by the full-encryption path. Stops at the first
    /// transport failure and returns the user-ids that did receive their email — the caller
    /// uses this set to exclude them from the compensating rollback (their invitation has
    /// already left our control and the code in their inbox must keep validating).
    /// </summary>
    private async Task<IReadOnlyList<int>> TrySendInvitationEmails(
        UserContext inviter,
        WorkspaceContext workspace,
        IReadOnlyList<CreateWorkspaceMemberInvitationQuery.PendingSyncEmail> pendingEmails,
        CancellationToken cancellationToken)
    {
        var sentUserIds = new List<int>(pendingEmails.Count);
        var sender = emailProviderStore.EmailSender;

        if (sender is null)
        {
            // Provider was deactivated between the pre-flight check and now — no recipient
            // has been emailed; the caller will roll back every invitee in the batch.
            Log.Error(
                "Email provider became unavailable between commit and synchronous send for " +
                "Workspace#{WorkspaceId} invitation. Initiating compensating rollback.",
                workspace.Id);
            return sentUserIds;
        }

        foreach (var pending in pendingEmails)
        {
            try
            {
                var (title, content) = Emails.WorkspaceMembershipInvitation(
                    applicationName: appSettings.ApplicationName.Name!,
                    appUrl: config.AppUrl,
                    inviterEmail: inviter.Email.Value,
                    workspaceName: workspace.Name,
                    invitationCode: pending.InvitationCode);

                var html = genericEmailTemplate.Build(
                    title: title,
                    content: content);

                await sender.SendEmail(
                    to: pending.InviteeEmail,
                    subject: title,
                    htmlContent: html,
                    cancellationToken: cancellationToken);

                sentUserIds.Add(pending.MemberId);
            }
            catch (Exception e)
            {
                Log.Error(e,
                    "Synchronous invitation email send failed for invitee '{Email}' on " +
                    "full-encryption Workspace#{WorkspaceId}. Initiating compensating rollback " +
                    "for the failed invitee and the remaining unsent ones.",
                    EmailAnonymization.Anonymize(pending.InviteeEmail),
                    workspace.Id);

                return sentUserIds;
            }
        }

        return sentUserIds;
    }

    private StagedInvitation ExecuteOperation(
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
            var rollbackTracker = new RollbackEncryptedInvitationQuery.ArtifactsTracker(
                workspaceId: workspace.Id);

            var users = memberEmails
                .Select(email =>
                {
                    var user = getOrCreateUserInvitationQuery.ExecuteTransaction(
                        dbWriteContext: dbWriteContext,
                        transaction: transaction,
                        email: email);

                    // The query only returns a non-null InvitationCode when it inserted a fresh
                    // u_users row (existing rows go through the SELECT branch which sets it to
                    // null). That makes InvitationCode our just-created sentinel.
                    if (user.InvitationCode is not null)
                        rollbackTracker.TrackNewlyCreatedUser(user.Id);

                    return user;
                })
                .ToList();

            var insertResult = createWorkspaceMemberInvitationQuery.ExecuteTransaction(
                dbWriteContext: dbWriteContext,
                transaction: transaction,
                workspace: workspace,
                inviter: inviter,
                members: users
                    .Select(user => new CreateWorkspaceMemberInvitationQuery.Member(
                        Id: user.Id,
                        Email: user.Email,
                        InvitationCode: user.InvitationCode))
                    .ToList(),
                allowShare: allowShare,
                correlationId: correlationId);

            foreach (var memberId in insertResult.NewlyInvitedMemberIds)
                rollbackTracker.TrackNewlyInvitedMember(memberId);

            var shouldGrantFullEncryptionAccess =
                workspace.EncryptionType == StorageEncryptionType.Full
                && ownerSession is not null;

            if (shouldGrantFullEncryptionAccess)
            {
                var newlyInvitedUsers = users
                    .Where(member => insertResult.NewlyInvitedMemberIds.Contains(
                        member.Id))
                    .ToList();

                var (autoGrantedCount, ephemeralGrantedCount) = GrantAccessToFullyEncryptedWorkspace(
                    dbWriteContext: dbWriteContext,
                    workspace: workspace,
                    inviter: inviter,
                    ownerSession: ownerSession!,
                    ephemeralDekLifetime: ephemeralDekLifetime,
                    correlationId: correlationId,
                    newlyInvitedMembers: newlyInvitedUsers,
                    rollbackTracker: rollbackTracker,
                    transaction: transaction);

                Log.Information(
                    "Workspace#{WorkspaceId} invitation by Inviter '{InviterId}' completed. " +
                    "Inserted {InsertedCount} of {RequestedCount} membership(s); " +
                    "auto-granted encryption access to {AutoGrantedCount} invitee(s); " +
                    "staged {EphemeralGrantedCount} ephemeral DEK(s).",
                    workspace.Id,
                    inviter.Id,
                    insertResult.NewlyInvitedMemberIds.Count,
                    users.Count,
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
                    users.Count);
            }

            transaction.Commit();

            return new StagedInvitation(
                Members: users,
                PendingSyncEmails: insertResult.PendingSyncEmails,
                RollbackTracker: rollbackTracker);
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
        List<GetOrCreateUserInvitationQuery.User> newlyInvitedMembers,
        RollbackEncryptedInvitationQuery.ArtifactsTracker rollbackTracker,
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
                target: new GrantEncryptionAccessOperation.TargetUser(
                    Id: grant.Target.Id,
                    Email: grant.Target.Email,
                    EncryptionMetadata: grant.Target.EncryptionMetadata),
                wrapped: grant.Wrapped,
                correlationId: correlationId,
                notifyTarget: false);

            rollbackTracker.TrackWek(grant.Target.Id);
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
                    correlationId: correlationId,
                    rollbackTracker: rollbackTracker);

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
        Guid correlationId,
        RollbackEncryptedInvitationQuery.ArtifactsTracker rollbackTracker)
    {
        var invitationCodeBytes = grant.InvitationCode is null
            ? null
            : Base62Encoding.FromBase62ToBytes(grant.InvitationCode.Value);

        try
        {
            var keypairResult = createOrGetEphemeralUserKeyPairQuery.ExecuteTransaction(
                dbWriteContext: dbWriteContext,
                userId: grant.Target.Id,
                invitationCodeBytes: invitationCodeBytes,
                createdByUserId: inviter.Id,
                transaction: transaction);

            if (keypairResult.PublicKey is null)
            {
                Log.Warning(
                    "Workspace#{WorkspaceId} ephemeral DEK staging skipped for User#{UserId}: " +
                    "no existing ephemeral keypair and no invitation code available to create one. " +
                    "User falls back to the deferred-grant path on encryption-password setup.",
                    workspace.Id, grant.Target.Id);

                return false;
            }

            if (keypairResult.WasJustCreated)
                rollbackTracker.TrackNewlyCreatedEuek(grant.Target.Id);

            foreach (var entry in ownerSession.Entries)
            {
                var sealedDek = entry.Dek.Use(
                    state: keypairResult.PublicKey.Value,
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

            rollbackTracker.TrackEwek(grant.Target.Id);
        }
        finally
        {
            if (invitationCodeBytes is not null)
                CryptographicOperations.ZeroMemory(invitationCodeBytes);
        }

        var debounceId = $"ewek-cleanup-{workspace.Id}-{grant.Target.Id}";

        queue.Enqueue(
            correlationId: correlationId,
            jobType: DeleteEphemeralWorkspaceEncryptionKeysQueueJobType.Value,
            definition: new DeleteEphemeralWorkspaceEncryptionKeysQueueJobDefinition
            {
                WorkspaceId = workspace.Id,
                UserId = grant.Target.Id
            },
            executeAfterDate: expiresAt,
            debounceId: debounceId,
            sagaId: null,
            batch: null,
            dbWriteContext: dbWriteContext,
            transaction: transaction);

        rollbackTracker.TrackCleanupJob(grant.Target.Id, debounceId);

        return true;
    }

    private static AutoGrant[] BuildAutoGrants(
        WorkspaceEncryptionSession ownerSession,
        IEnumerable<GetOrCreateUserInvitationQuery.User> members)
    {
        return members
            .Where(m => m.EncryptionMetadata is not null)
            .Select(m => new AutoGrant
            {
                Target = m,
                Wrapped = GrantEncryptionAccessOperation.BuildWrapped(
                    ownerSession: ownerSession,
                    target: new GrantEncryptionAccessOperation.TargetUser(
                        Id: m.Id,
                        Email: m.Email,
                        EncryptionMetadata: m.EncryptionMetadata))
            })
            .ToArray();
    }

    private static EphemeralGrant[] BuildEphemeralGrants(
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction,
        IEnumerable<GetOrCreateUserInvitationQuery.User> members)
    {
        var candidates = members
            .Where(m => m.EncryptionMetadata is null)
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
            .Where(candidate => candidate.InvitationCode is not null || existingEuekUserIds.Contains(candidate.Id))
            .ToArray();

        if (eligible.Length == 0)
            return [];

        return eligible
            .Select(m => new EphemeralGrant
            {
                Target = m,
                InvitationCode = m.InvitationCode
            })
            .ToArray();
    }


    private static HashSet<int> CandidatesWithExistingEuek(
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction,
        GetOrCreateUserInvitationQuery.User[] candidates)
    {
        var candidatesWithoutCode = candidates
            .Where(m => m.InvitationCode is null)
            .Select(m => m.Id)
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
        public required GetOrCreateUserInvitationQuery.User Target { get; init; }
        public required GrantEncryptionAccessOperation.WrappedVersion[] Wrapped { get; init; }
    }

    private class EphemeralGrant
    {
        public required GetOrCreateUserInvitationQuery.User Target { get; init; }
        public required InvitationCode? InvitationCode { get; init; }
    }

    /// <summary>
    /// Snapshot of what the DB write step produced: the invited users (returned to the
    /// caller), the invitations queued for synchronous send (FE-only), and the rollback
    /// tracker. The tracker is consumed (mutated) by the synchronous send loop — each
    /// successful send marks the recipient so its artifacts are excluded from the
    /// compensating delete if the loop later fails on another recipient.
    /// </summary>
    private sealed record StagedInvitation(
        List<GetOrCreateUserInvitationQuery.User> Members,
        List<CreateWorkspaceMemberInvitationQuery.PendingSyncEmail> PendingSyncEmails,
        RollbackEncryptedInvitationQuery.ArtifactsTracker RollbackTracker);

    public enum ResultCode
    {
        Ok,
        EmailProviderNotConfigured,
        EmailSendFailed
    }

    public readonly record struct Result(
        ResultCode Code,
        List<GetOrCreateUserInvitationQuery.User> Members);
}
