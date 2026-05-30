using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Files.Delete;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.MediaProcessing;

/// <summary>
/// Atomic hard-delete of a batch of thumbnail rows for a single workspace, with storage
/// cleanup jobs enqueued in the same transaction. The caller (typically
/// <see cref="DeleteFileThumbnailOperation"/>) resolves which thumbnails to delete (e.g. all
/// of a given variant for a given parent) and hands the file IDs over.
/// </summary>
public class DeleteThumbnailsQuery(
    DbWriteQueue dbWriteQueue,
    HardDeleteFilesWithStorageCleanupSubQuery hardDeleteFilesWithStorageCleanupSubQuery)
{
    public Task Execute(
        WorkspaceContext workspace,
        List<int> thumbnailFileIds,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        if (thumbnailFileIds.Count == 0)
            return Task.CompletedTask;

        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace: workspace,
                thumbnailFileIds: thumbnailFileIds,
                correlationId: correlationId),
            cancellationToken: cancellationToken);
    }

    private void ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        WorkspaceContext workspace,
        List<int> thumbnailFileIds,
        Guid correlationId)
    {
        var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            hardDeleteFilesWithStorageCleanupSubQuery.Execute(
                workspaceId: workspace.Id,
                fileIds: thumbnailFileIds,
                correlationId: correlationId,
                dbWriteContext: dbWriteContext,
                transaction: transaction);

            transaction.Commit();

            Log.Information(
                "Hard-deleted {Count} thumbnails in Workspace#{WorkspaceId}.",
                thumbnailFileIds.Count,
                workspace.Id);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
