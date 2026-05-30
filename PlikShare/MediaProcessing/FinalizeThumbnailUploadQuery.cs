using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Files.Delete;
using PlikShare.Files.Id;
using PlikShare.Files.UploadAttachment;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.MediaProcessing;

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
        EncryptableMetadata thumbnailMetadata,
        List<int> oldThumbnailFileIds,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace: workspace,
                newThumbnailExternalId: newThumbnailExternalId,
                thumbnailMetadata: thumbnailMetadata,
                oldThumbnailFileIds: oldThumbnailFileIds,
                correlationId: correlationId),
            cancellationToken: cancellationToken);
    }

    private void ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        WorkspaceContext workspace,
        FileExtId newThumbnailExternalId,
        EncryptableMetadata thumbnailMetadata,
        List<int> oldThumbnailFileIds,
        Guid correlationId)
    {
        var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            UpdateThumbnailMetadata(
                dbWriteContext: dbWriteContext,
                transaction: transaction,
                workspace: workspace,
                thumbnailExternalId: newThumbnailExternalId,
                thumbnailMetadata: thumbnailMetadata);

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

    private static void UpdateThumbnailMetadata(
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction,
        WorkspaceContext workspace,
        FileExtId thumbnailExternalId,
        EncryptableMetadata thumbnailMetadata)
    {
        dbWriteContext
            .OneRowCmd(
                sql: @"
                    UPDATE fi_files
                    SET fi_metadata = $metadata
                    WHERE fi_external_id = $externalId
                        AND fi_workspace_id = $workspaceId
                    RETURNING fi_id",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$externalId", thumbnailExternalId.Value)
            .WithParameter("$workspaceId", workspace.Id)
            .WithEncryptableBlobParameter("$metadata", thumbnailMetadata)
            .Execute();
    }
}
