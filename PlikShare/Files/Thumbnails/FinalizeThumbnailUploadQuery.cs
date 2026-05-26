using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Files.Delete;
using PlikShare.Files.Id;
using PlikShare.Files.UploadAttachment;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Files.Thumbnails;

/// <summary>
/// Atomic switchover at the end of a thumbnail upload: marks the freshly-uploaded thumbnail
/// as completed and hard-deletes any previous thumbnails of the same variant in a single
/// transaction. Storage cleanup jobs for the old thumbs land in the same transaction —
/// either everything commits or nothing does. If the storage upload step before this query
/// failed, the new row stays incomplete and the old thumb is untouched.
/// </summary>
public class FinalizeThumbnailUploadQuery(
    DbWriteQueue dbWriteQueue,
    MarkFileAsUploadedQuery markFileAsUploadedQuery,
    HardDeleteFilesWithStorageCleanupSubQuery hardDeleteFilesWithStorageCleanupSubQuery)
{
    public Task Execute(
        WorkspaceContext workspace,
        FileExtId newThumbnailExternalId,
        List<int> oldThumbnailFileIds,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace: workspace,
                newThumbnailExternalId: newThumbnailExternalId,
                oldThumbnailFileIds: oldThumbnailFileIds,
                correlationId: correlationId),
            cancellationToken: cancellationToken);
    }

    private void ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        WorkspaceContext workspace,
        FileExtId newThumbnailExternalId,
        List<int> oldThumbnailFileIds,
        Guid correlationId)
    {
        var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            markFileAsUploadedQuery.ExecuteInTransaction(
                dbWriteContext: dbWriteContext,
                transaction: transaction,
                fileExternalId: newThumbnailExternalId);

            hardDeleteFilesWithStorageCleanupSubQuery.Execute(
                workspaceId: workspace.Id,
                fileIds: oldThumbnailFileIds,
                correlationId: correlationId,
                dbWriteContext: dbWriteContext,
                transaction: transaction);

            transaction.Commit();

            Log.Information(
                "Finalized thumbnail upload '{NewThumbnailExternalId}'. Replaced {OldCount} old thumbnails in Workspace#{WorkspaceId}.",
                newThumbnailExternalId,
                oldThumbnailFileIds.Count,
                workspace.Id);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
