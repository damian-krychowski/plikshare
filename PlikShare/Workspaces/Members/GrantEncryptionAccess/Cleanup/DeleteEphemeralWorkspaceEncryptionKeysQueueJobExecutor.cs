using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using Serilog;

namespace PlikShare.Workspaces.Members.GrantEncryptionAccess.Cleanup;

/// <summary>
/// Deletes any ephemeral workspace encryption keys still pending for a
/// (workspace, user) pair after the owner-chosen TTL elapses. The job is idempotent:
/// if the invitee already promoted their ephemeral wraps to <c>wek_*</c> during
/// encryption-password setup, the DELETE matches zero rows and the job still
/// succeeds. No side effects beyond the DB write.
/// </summary>
public class DeleteEphemeralWorkspaceEncryptionKeysQueueJobExecutor : IQueueNormalJobExecutor
{
    public static string StaticJobType => DeleteEphemeralWorkspaceEncryptionKeysQueueJobType.Value;
    public static int StaticPriority => QueueJobPriority.ExtremelyLow;

    public string JobType => StaticJobType;
    public int Priority => StaticPriority;

    public Task<QueueJobResult> Execute(
        string definitionJson,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var definition = Json.Deserialize<DeleteEphemeralWorkspaceEncryptionKeysQueueJobDefinition>(
            definitionJson);

        if (definition is null)
        {
            throw new ArgumentException(
                $"Job '{definitionJson}' cannot be parsed to correct '{nameof(DeleteEphemeralWorkspaceEncryptionKeysQueueJobDefinition)}'");
        }

        return Task.FromResult(QueueJobResult.SuccessWithDbWrite(
            dbWrite: (dbWriteContext, transaction) =>
            {
                var result = dbWriteContext
                    .Connection
                    .NonQueryCmd(
                        sql: """
                             DELETE FROM ewek_ephemeral_workspace_encryption_keys
                             WHERE ewek_workspace_id = $workspaceId
                               AND ewek_user_id = $userId
                             """,
                        transaction: transaction)
                    .WithParameter("$workspaceId", definition.WorkspaceId)
                    .WithParameter("$userId", definition.UserId)
                    .Execute();

                Log.Information(
                    "Ephemeral workspace encryption keys cleanup for Workspace#{WorkspaceId} User#{UserId} removed {DeletedCount} row(s).",
                    definition.WorkspaceId, definition.UserId, result.AffectedRows);
            }));
    }
}
