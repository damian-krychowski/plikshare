using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Workspaces.Members.CreateInvitation;

/// <summary>
/// Compensating delete for the full-encryption workspace member invite path. Runs in a
/// single transaction after a sync invitation email send has failed. Deletes ONLY the
/// rows we are sure were created by the immediately-preceding invite operation — the
/// caller tracks them as it writes (see <see cref="Artifacts"/>). Anything that pre-
/// existed the operation is left alone.
///
/// Why this exists: plaintext invitation codes double as KEKs for euek-wrapped
/// ephemeral private keys (<see cref="PlikShare.Core.Encryption.InvitationCodePrivateKeyWrap"/>).
/// The FE workspace invite path therefore sends the email synchronously after commit
/// rather than enqueuing it — otherwise the plaintext would persist in
/// <c>q_queue.q_definition</c> (and after success in <c>qc_queue_completed.qc_definition</c>)
/// indefinitely. If the synchronous send fails the inviter sees an error and the staged
/// DB state — membership rows, ephemeral keypair, ephemeral wraps, TTL cleanup jobs —
/// must be unwound so the invitee cannot end up "ghost-invited".
/// </summary>
public class RollbackEncryptedInvitationQuery(DbWriteQueue dbWriteQueue)
{
    public Task Execute(
        Artifacts artifacts,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                artifacts: artifacts),
            cancellationToken: cancellationToken);
    }

    private object? ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        Artifacts artifacts)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            // 1. Cleanup TTL queue jobs for the staged eweks — by debounce id which we know
            //    follows the deterministic `ewek-cleanup-{wId}-{uId}` shape.
            foreach (var debounceId in artifacts.CleanupJobDebounceIds)
            {
                dbWriteContext
                    .Cmd(
                        sql: "DELETE FROM q_queue WHERE q_debounce_id = $debounceId RETURNING q_id",
                        readRowFunc: static r => r.GetInt32(0),
                        transaction: transaction)
                    .WithParameter("$debounceId", debounceId)
                    .Execute();
            }

            // 2. ewek rows — by (workspaceId, userId). FK cascade on workspace delete would
            //    eventually clear these, but we don't want to wait.
            foreach (var (workspaceId, userId) in artifacts.EwekKeys)
            {
                dbWriteContext
                    .Cmd(
                        sql: """
                             DELETE FROM ewek_ephemeral_workspace_encryption_keys
                             WHERE ewek_workspace_id = $workspaceId AND ewek_user_id = $userId
                             RETURNING ewek_user_id
                             """,
                        readRowFunc: static r => r.GetInt32(0),
                        transaction: transaction)
                    .WithParameter("$workspaceId", workspaceId)
                    .WithParameter("$userId", userId)
                    .Execute();
            }

            // 3. euek rows — only those whose keypair was just created in this operation.
            //    Existing euek rows shared with prior invitations are not ours to delete.
            foreach (var userId in artifacts.NewlyCreatedEuekUserIds)
            {
                dbWriteContext
                    .Cmd(
                        sql: """
                             DELETE FROM euek_ephemeral_user_encryption_keys
                             WHERE euek_user_id = $userId
                             RETURNING euek_user_id
                             """,
                        readRowFunc: static r => r.GetInt32(0),
                        transaction: transaction)
                    .WithParameter("$userId", userId)
                    .Execute();
            }

            // 4. wek rows — the auto-grant wraps for already-encrypted invitees. Limited to
            //    (workspaceId, userId) pairs from this operation.
            foreach (var (workspaceId, userId) in artifacts.WekKeys)
            {
                dbWriteContext
                    .Cmd(
                        sql: """
                             DELETE FROM wek_workspace_encryption_keys
                             WHERE wek_workspace_id = $workspaceId AND wek_user_id = $userId
                             RETURNING wek_user_id
                             """,
                        readRowFunc: static r => r.GetInt32(0),
                        transaction: transaction)
                    .WithParameter("$workspaceId", workspaceId)
                    .WithParameter("$userId", userId)
                    .Execute();
            }

            // 5. wm_workspace_membership — by (workspaceId, memberId) from NewlyInvitedMemberIds.
            foreach (var memberId in artifacts.NewlyInvitedMemberIds)
            {
                dbWriteContext
                    .Cmd(
                        sql: """
                             DELETE FROM wm_workspace_membership
                             WHERE wm_workspace_id = $workspaceId AND wm_member_id = $memberId
                             RETURNING wm_member_id
                             """,
                        readRowFunc: static r => r.GetInt32(0),
                        transaction: transaction)
                    .WithParameter("$workspaceId", artifacts.WorkspaceId)
                    .WithParameter("$memberId", memberId)
                    .Execute();
            }

            // 6. u_users — only for invitees freshly created in this operation. Reused user
            //    rows from prior admin/workspace invites pre-existed and must stay.
            foreach (var userId in artifacts.NewlyCreatedUserIds)
            {
                dbWriteContext
                    .Cmd(
                        sql: "DELETE FROM u_users WHERE u_id = $userId AND u_is_invitation = TRUE RETURNING u_id",
                        readRowFunc: static r => r.GetInt32(0),
                        transaction: transaction)
                    .WithParameter("$userId", userId)
                    .Execute();
            }

            transaction.Commit();

            Log.Information(
                "Rolled back encrypted Workspace#{WorkspaceId} invitation. " +
                "Deleted memberships: {Memberships}, eweks: {Eweks}, eueks: {Eueks}, " +
                "weks: {Weks}, users: {Users}, cleanup jobs: {Jobs}.",
                artifacts.WorkspaceId,
                artifacts.NewlyInvitedMemberIds.Count,
                artifacts.EwekKeys.Count,
                artifacts.NewlyCreatedEuekUserIds.Count,
                artifacts.WekKeys.Count,
                artifacts.NewlyCreatedUserIds.Count,
                artifacts.CleanupJobDebounceIds.Count);

            return null;
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e,
                "Failed to rollback encrypted Workspace#{WorkspaceId} invitation after sync " +
                "email send failure. DB state may be inconsistent — invited members count: " +
                "{InvitedCount}.",
                artifacts.WorkspaceId,
                artifacts.NewlyInvitedMemberIds.Count);

            throw;
        }
    }

    public sealed record Artifacts(
        int WorkspaceId,
        IReadOnlyList<int> NewlyCreatedUserIds,
        IReadOnlyList<int> NewlyCreatedEuekUserIds,
        IReadOnlyList<(int WorkspaceId, int UserId)> EwekKeys,
        IReadOnlyList<(int WorkspaceId, int UserId)> WekKeys,
        IReadOnlyList<int> NewlyInvitedMemberIds,
        IReadOnlyList<string> CleanupJobDebounceIds);

    /// <summary>
    /// Mutable accumulator threaded through the invitation write pipeline. Each step that
    /// inserts something into a table that needs unwinding on rollback registers it on the
    /// per-invitee bucket (<see cref="InviteeArtifacts"/>) for the relevant user-id.
    ///
    /// <see cref="BuildRollbackArtifacts"/> emits an <see cref="Artifacts"/> snapshot
    /// covering only the invitees the caller has NOT excluded. The caller's
    /// synchronous-send loop excludes recipients that received their invitation — their
    /// invitation stands and the staged rows must remain in place.
    /// </summary>
    public sealed class ArtifactsTracker
    {
        private readonly int _workspaceId;
        private readonly Dictionary<int, InviteeArtifacts> _byUser = [];

        public ArtifactsTracker(int workspaceId)
        {
            _workspaceId = workspaceId;
        }

        public void TrackNewlyInvitedMember(int userId) => GetOrCreate(userId).WasNewlyInvited = true;

        public void TrackNewlyCreatedUser(int userId) => GetOrCreate(userId).WasUserCreated = true;

        public void TrackNewlyCreatedEuek(int userId) => GetOrCreate(userId).WasEuekCreated = true;

        public void TrackEwek(int userId) => GetOrCreate(userId).HasEwek = true;

        public void TrackWek(int userId) => GetOrCreate(userId).HasWek = true;

        public void TrackCleanupJob(int userId, string debounceId) => GetOrCreate(userId).CleanupJobDebounceId = debounceId;

        public Artifacts BuildRollbackArtifacts(IReadOnlyCollection<int> userIdsToExclude)
        {
            var excludeSet = userIdsToExclude as HashSet<int> ?? [..userIdsToExclude];

            var toRollback = _byUser
                .Values
                .Where(bucket => !excludeSet.Contains(bucket.UserId))
                .ToList();

            return new Artifacts(
                WorkspaceId: _workspaceId,
                NewlyCreatedUserIds: toRollback
                    .Where(b => b.WasUserCreated)
                    .Select(b => b.UserId)
                    .ToList(),
                NewlyCreatedEuekUserIds: toRollback
                    .Where(b => b.WasEuekCreated)
                    .Select(b => b.UserId)
                    .ToList(),
                EwekKeys: toRollback
                    .Where(b => b.HasEwek)
                    .Select(b => (_workspaceId, b.UserId))
                    .ToList(),
                WekKeys: toRollback
                    .Where(b => b.HasWek)
                    .Select(b => (_workspaceId, b.UserId))
                    .ToList(),
                NewlyInvitedMemberIds: toRollback
                    .Where(b => b.WasNewlyInvited)
                    .Select(b => b.UserId)
                    .ToList(),
                CleanupJobDebounceIds: toRollback
                    .Where(b => b.CleanupJobDebounceId is not null)
                    .Select(b => b.CleanupJobDebounceId!)
                    .ToList());
        }

        private InviteeArtifacts GetOrCreate(int userId)
        {
            if (_byUser.TryGetValue(userId, out var existing))
                return existing;

            var bucket = new InviteeArtifacts(userId);
            _byUser[userId] = bucket;
            return bucket;
        }

        /// <summary>
        /// Per-invitee bundle of "things we wrote that may need to be unwound". One bucket
        /// per user id; flat boolean flags rather than separate collections because at most
        /// one row of each kind is ever staged per (workspace, user) pair in this operation.
        /// </summary>
        private sealed class InviteeArtifacts(int userId)
        {
            public int UserId { get; } = userId;
            public bool WasUserCreated { get; set; }
            public bool WasEuekCreated { get; set; }
            public bool HasEwek { get; set; }
            public bool HasWek { get; set; }
            public bool WasNewlyInvited { get; set; }
            public string? CleanupJobDebounceId { get; set; }
        }
    }
}
