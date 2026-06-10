using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Files.Delete;
using PlikShare.Workspaces.UpdateCurrentSizeInBytes.QueueJob;

namespace PlikShare.Trash;

// Hard-deletes a set of files and enqueues the two follow-up jobs that must always accompany a
// physical delete: the storage-object purge and the workspace-size recompute. Bundling them
// keeps the invariant "deleting files always schedules its cleanup" enforced by the API shape —
// a caller can't forget one. Runs inside the caller's transaction.
public class PurgeFilesSubQuery(
    DeleteFilesSubQuery deleteFilesSubQuery,
    IQueue queue,
    IClock clock)
{
    public int Execute(
        int workspaceId,
        List<int> fileIds,
        Guid correlationId,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        if (fileIds.Count == 0)
            return 0;

        var (deletedFiles, jobs) = deleteFilesSubQuery.Execute(
            workspaceId: workspaceId,
            fileIds: fileIds,
            sagaId: null,
            dbWriteContext: dbWriteContext,
            transaction: transaction);

        queue.EnqueueBulk(
            correlationId: correlationId,
            definitions: jobs,
            executeAfterDate: clock.UtcNow,
            workspaceId: workspaceId,
            dbWriteContext: dbWriteContext,
            transaction: transaction);

        queue.EnqueueWorkspaceSizeUpdateJob(
            clock: clock,
            workspaceId: workspaceId,
            correlationId: correlationId,
            dbWriteContext: dbWriteContext,
            transaction: transaction);

        return deletedFiles.Count;
    }
}
