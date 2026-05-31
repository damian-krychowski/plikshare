using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Workspaces.Cache;

namespace PlikShare.MediaProcessing.Generation;

/// <summary>
/// Cancels a thumbnail batch by deleting its still-Pending queue jobs. Jobs already Processing
/// finish naturally (so files mid-flight still get their thumbnail) and completed thumbnails stay.
/// Each deleted job's temporary workspace encryption key is released so it doesn't linger to TTL.
/// </summary>
public class CancelThumbnailBatchOperation(
    DbWriteQueue dbWriteQueue,
    TemporaryWorkspaceEncryptionKeyStore keyStore,
    QueueBatchNotifier batchNotifier)
{
    public async Task<int> Execute(
        WorkspaceContext workspace,
        Guid batchId,
        CancellationToken cancellationToken)
    {
        var releasedKeyIds = await dbWriteQueue.Execute(
            operationToEnqueue: context => DeletePendingJobs(
                context: context,
                workspace: workspace,
                batchId: batchId),
            cancellationToken: cancellationToken);

        foreach (var keyId in releasedKeyIds)
            if (keyId is { } id)
                keyStore.Remove(id);

        batchNotifier.Notify(batchId);

        return releasedKeyIds.Count;
    }

    private static List<Guid?> DeletePendingJobs(
        SqliteWriteContext context,
        WorkspaceContext workspace,
        Guid batchId)
    {
        return context
            .Cmd(
                sql: """
                    DELETE FROM q_queue
                    WHERE q_batch_id = $batchId
                        AND json_extract(q_definition, '$.workspaceId') = $workspaceId
                        AND q_status = $pendingStatus
                    RETURNING q_definition
                    """,
                readRowFunc: reader =>
                {
                    var definition = Json.Deserialize<ProcessImageQueueJobDefinition>(
                        reader.GetString(0));

                    return definition?.TempEncryptionKeyId;
                })
            .WithParameter("$batchId", batchId)
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$pendingStatus", QueueStatus.Pending)
            .Execute();
    }
}
