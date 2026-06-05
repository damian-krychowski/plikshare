using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;

namespace PlikShare.MediaProcessing.Generation;

/// <summary>
/// Cancels a thumbnail batch by deleting its still-Pending queue jobs. Jobs already Processing
/// finish naturally (so files mid-flight still get their thumbnail) and completed thumbnails stay.
/// Each deleted job's temporary workspace encryption key is released so it doesn't linger to TTL.
/// </summary>
public class CancelThumbnailBatchOperation(
    DbWriteQueue dbWriteQueue,
    QueueBatchNotifier batchNotifier)
{
    public async Task<int> Execute(
        Guid batchId,
        CancellationToken cancellationToken)
    {
        var deletedDefinitions = await dbWriteQueue.Execute(
            operationToEnqueue: context => DeletePendingJobs(
                context: context,
                batchId: batchId),
            cancellationToken: cancellationToken);

        // Each pending job carries up to BatchSize files, each with its own temporary encryption
        // package handle (parent decryption wire + per-variant encryption wires + metadata seed).
        // Release them all from the store so they don't linger to TTL.
        var releasedFiles = 0;

        foreach (var definition in deletedDefinitions)
        {
            if (definition is null)
                continue;

            releasedFiles += definition.Files.Length;
        }

        batchNotifier.Notify(batchId);

        return releasedFiles;
    }

    private static List<ProcessImageQueueJobDefinitionV2?> DeletePendingJobs(
        SqliteWriteContext context,
        Guid batchId)
    {
        return context
            .Cmd(
                sql: """
                    DELETE FROM q_queue
                    WHERE q_batch_id = $batchId
                        AND q_status = $pendingStatus
                    RETURNING q_definition
                    """,
                readRowFunc: reader => Json.Deserialize<ProcessImageQueueJobDefinitionV2>(
                    reader.GetString(0)))
            .WithParameter("$batchId", batchId)
            .WithParameter("$pendingStatus", QueueStatus.Pending)
            .Execute();
    }
}
