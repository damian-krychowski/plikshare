using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;

namespace PlikShare.Files.Delete;

/// <summary>
/// Combines <see cref="DeleteFilesSubQuery"/> (hard-delete rows + collect storage cleanup
/// jobs) with the enqueue step in one call, inside the caller's transaction. Use it from
/// flows that hard-delete a small batch of files and want the storage cleanup queued
/// atomically with the delete — if the transaction rolls back, the queue jobs vanish too.
/// Not a replacement for <see cref="BulkDeleteQuery"/>, which accumulates jobs across many
/// sub-queries (files + folders + uploads) and enqueues once at the end.
/// </summary>
public class HardDeleteFilesWithStorageCleanupSubQuery(
    IClock clock,
    IQueue queue,
    DeleteFilesSubQuery deleteFilesSubQuery)
{
    public void Execute(
        int workspaceId,
        List<int> fileIds,
        Guid correlationId,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        if (fileIds.Count == 0)
            return;

        var (_, jobsToEnqueue) = deleteFilesSubQuery.Execute(
            workspaceId: workspaceId,
            fileIds: fileIds,
            sagaId: null,
            dbWriteContext: dbWriteContext,
            transaction: transaction);

        if (jobsToEnqueue.Count == 0)
            return;

        queue.EnqueueBulk(
            correlationId: correlationId,
            definitions: jobsToEnqueue,
            executeAfterDate: clock.UtcNow,
            workspaceId: workspaceId,
            dbWriteContext: dbWriteContext,
            transaction: transaction);
    }
}
