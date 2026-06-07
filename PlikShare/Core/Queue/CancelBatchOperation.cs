using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.Core.Queue;

// Cancels a batch by deleting its still-Pending queue jobs. Jobs already Processing finish
// naturally; completed work stays. Generic over job type (no definition parsing), so any batch can
// be cancelled by id. Returns the number of pending jobs removed.
public class CancelBatchOperation(
    DbWriteQueue dbWriteQueue,
    QueueBatchNotifier batchNotifier)
{
    public async Task<int> Execute(
        Guid batchId,
        CancellationToken cancellationToken)
    {
        var deletedCount = await dbWriteQueue.Execute(
            operationToEnqueue: context => DeletePendingJobs(
                context: context,
                batchId: batchId),
            cancellationToken: cancellationToken);

        batchNotifier.Notify(batchId);

        return deletedCount;
    }

    private static int DeletePendingJobs(
        SqliteWriteContext context,
        Guid batchId)
    {
        return context
            .Cmd(
                sql: """
                    DELETE FROM q_queue
                    WHERE q_batch_id = $batchId
                        AND q_status = $pendingStatus
                    RETURNING q_id
                    """,
                readRowFunc: reader => reader.GetInt64(0))
            .WithParameter("$batchId", batchId)
            .WithParameter("$pendingStatus", QueueStatus.Pending)
            .Execute()
            .Count;
    }
}
