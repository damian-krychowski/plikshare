using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.Delete;
using PlikShare.Files.Id;
using PlikShare.Folders.Delete.QueueJob;
using PlikShare.Folders.Id;
using PlikShare.Integrations.Aws.Textract.Jobs.Delete;
using PlikShare.Storages.FileCopying.Delete;
using PlikShare.Uploads.Delete;
using PlikShare.Uploads.Id;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.UpdateCurrentSizeInBytes.QueueJob;
using Serilog;
using Serilog.Events;

namespace PlikShare.BulkDelete;

public class BulkDeleteQuery(
    IClock clock,
    IQueue queue,
    DbWriteQueue dbWriteQueue,
    DeleteFilesSubQuery deleteFilesSubQuery,
    DeleteFileUploadsSubQuery deleteFileUploadsSubQuery,
    DeleteCopyFileQueueJobsSubQuery deleteCopyFileQueueJobsSubQuery,
    DeleteTextractJobsSubQuery deleteTextractJobsSubQuery)
{
    public Task Execute(
        WorkspaceContext workspace,
        FileExtId[] fileExternalIds,
        FolderExtId[] folderExternalIds,
        FileUploadExtId[] fileUploadExternalIds,
        int? boxFolderId,
        IUserIdentity userIdentity,
        bool isFileDeleteAllowedByBoxPermissions,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace: workspace, 
                fileExternalIds: fileExternalIds, 
                folderExternalIds: folderExternalIds, 
                fileUploadExternalIds: fileUploadExternalIds, 
                boxFolderId: boxFolderId, 
                userIdentity: userIdentity,
                isFileDeleteAllowedByBoxPermissions: isFileDeleteAllowedByBoxPermissions, 
                correlationId: correlationId),
            cancellationToken: cancellationToken);
    }


    private void ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        WorkspaceContext workspace,
        FileExtId[] fileExternalIds,
        FolderExtId[] folderExternalIds,
        FileUploadExtId[] fileUploadExternalIds,
        int? boxFolderId,
        IUserIdentity userIdentity,
        bool isFileDeleteAllowedByBoxPermissions,
        Guid correlationId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();
        
        try
        {
            dbWriteContext.DeferForeignKeys(
                transaction: transaction);
            
            var jobsToEnqueue = new List<BulkQueueJobEntity>();

            var filesToDelete = GetFilesToDelete(
                workspace,
                fileExternalIds,
                boxFolderId,
                userIdentity,
                isFileDeleteAllowedByBoxPermissions,
                dbWriteContext,
                transaction);

            var (deletedFiles, fileJobs) = deleteFilesSubQuery.Execute(
                workspaceId: workspace.Id,
                fileIds: filesToDelete,
                sagaId: null,
                dbWriteContext: dbWriteContext,
                transaction: transaction);
            
            jobsToEnqueue.AddRange(fileJobs);

            var deletedFileIds = deletedFiles
                .Select(df => df.Id)
                .ToArray();

            var fileUploadsToDelete = GetFileUploadsToDelete(
                workspace, 
                fileUploadExternalIds, 
                userIdentity, 
                dbWriteContext, 
                transaction);

            var (deletedFileUploads, deletedFileUploadParts, fileUploadsJobs) = deleteFileUploadsSubQuery.Execute(
                fileUploadsToDelete,
                sagaId: null,
                dbWriteContext, 
                transaction);

            jobsToEnqueue.AddRange(fileUploadsJobs);

            var (foldersMarkedAsBeingDeleted, foldersJobs) = MarkFoldersForDelete(
                workspace,
                folderExternalIds,
                boxFolderId,
                dbWriteContext,
                transaction);

            jobsToEnqueue.AddRange(foldersJobs);

            var (deletedCopyFileQueueJobs, deletedCopyFileUploads, deletedCopyFileUploadParts, copyFileUploadJobs) =
                deleteCopyFileQueueJobsSubQuery.Execute(
                    workspaceId: workspace.Id,
                    deletedFileIds: deletedFileIds,
                    deletedFileUploadIds: deletedFileUploads.Select(dfu => dfu.Id).ToArray(),
                    dbWriteContext: dbWriteContext,
                    transaction: transaction);
            
            jobsToEnqueue.AddRange(copyFileUploadJobs);

            var deletedTextractJobs = deleteTextractJobsSubQuery.Execute(
                workspaceId: workspace.Id,
                deletedFileIds: deletedFileIds,
                dbWriteContext: dbWriteContext,
                transaction: transaction);

            var queueJobs = queue.EnqueueBulk(
                correlationId: correlationId,
                definitions: jobsToEnqueue,
                executeAfterDate: clock.UtcNow,
                dbWriteContext: dbWriteContext,
                transaction: transaction);
            
            var updateWorkspaceCurrentSizeJob = queue.EnqueueOrThrow(
                correlationId: correlationId,
                jobType: UpdateWorkspaceCurrentSizeInBytesQueueJobType.Value,
                definition: new UpdateWorkspaceCurrentSizeInBytesQueueJobDefinition(
                    WorkspaceId: workspace.Id),
                executeAfterDate: clock.UtcNow.AddSeconds(10),
                debounceId: $"update_workspace_current_size_in_bytes_{workspace.Id}",
                sagaId: null,
                dbWriteContext: dbWriteContext,
                transaction: transaction);

            queueJobs.Add(updateWorkspaceCurrentSizeJob);
            
            transaction.Commit();

            if (Log.IsEnabled(LogEventLevel.Information))
            {
                var fileIds = IdsRange.GroupConsecutiveIds(
                    ids: deletedFiles.Select(x => x.Id));

                var fileUploadIds = IdsRange.GroupConsecutiveIds(
                    ids: deletedFileUploads.Select(x => x.Id).Concat(deletedCopyFileUploads.Select(x => x.Id)));

                var folderIds = IdsRange.GroupConsecutiveIds(
                    ids: foldersMarkedAsBeingDeleted);

                var queueJobIds = IdsRange.GroupConsecutiveIds(
                    ids: queueJobs.Select(x => x.Value));

                var copyFileQueueJobIds = IdsRange.GroupConsecutiveIds(
                    ids: deletedCopyFileQueueJobs.Select(x => x.Id));

                var textractJobIds = IdsRange.GroupConsecutiveIds(
                    ids: deletedTextractJobs.Select(x => x.Id));

                Log.Information(
                    "Bulk delete operation finished. " +
                    "Deleted Folders ({FoldersCount}): {FolderIds}, " +
                    "Files ({FilesCount}): [{FileIds}], " +
                    "FileUploads ({FileUploadsCount}): [{FileUploadIds}], " +
                    "FileUploadParts ({FileUploadPartsCount}). " +
                    "CopyFileQueueJobs ({CopyFileQueueJobsCount}): [{CopyFileQueueJobIds}]" +
                    "TextractJobs ({TextractJobsCount}): [{TextractJobIds}]" +
                    "Enqueued jobs ({QueueJobsCount}): [{QueueJobIds}]",
                    foldersMarkedAsBeingDeleted.Count, 
                    folderIds,
                    deletedFiles.Count,
                    fileIds,
                    deletedFileUploads.Count, 
                    fileUploadIds, 
                    deletedFileUploadParts.Count + deletedCopyFileUploadParts.Count,
                    deletedCopyFileQueueJobs.Count,
                    copyFileQueueJobIds,
                    deletedTextractJobs.Count,
                    textractJobIds,
                    queueJobs.Count,
                    queueJobIds);
            }
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something whet wrong while deleting Files '{FileExternalIds}'",
                fileExternalIds);
            
            throw;
        }
    }
    
    private static List<int> GetFilesToDelete(
        WorkspaceContext workspace,
        FileExtId[] fileExternalIds,
        int? boxFolderId,
        IUserIdentity userIdentity,
        bool isFileDeleteAllowedByBoxPermissions,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        if (fileExternalIds.Length == 0)
            return [];

        return dbWriteContext
            .Cmd(
                sql: @"
                    SELECT fi_id
                    FROM fi_files
                    LEFT JOIN fo_folders
                        ON fo_id = fi_folder_id
                        AND fo_workspace_id = $workspaceId
                    WHERE
                        fi_external_id IN (
                            SELECT value FROM json_each($fileExternalIds)
                        )
                        AND fi_workspace_id = $workspaceId
                        AND (fi_folder_id IS NULL OR fo_is_being_deleted = FALSE)
                        AND (
                            $boxFolderId IS NULL
                            OR $isFileDeleteAllowedByBoxPermissions = TRUE 
                            OR (
                                fi_uploader_identity = $uploaderIdentity
                                AND fi_uploader_identity_type = $uploaderIdentityType
                            )
                        )
                        AND (
                            $boxFolderId IS NULL 
                            OR $boxFolderId = fo_id 
                            OR $boxFolderId IN (
                                SELECT value FROM json_each(fo_ancestor_folder_ids) 
                            )   
                        )
                ",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$workspaceId", workspace.Id)
            .WithJsonParameter("$fileExternalIds", fileExternalIds)
            .WithParameter("$boxFolderId", boxFolderId)
            .WithParameter("$isFileDeleteAllowedByBoxPermissions", isFileDeleteAllowedByBoxPermissions)
            .WithParameter("$uploaderIdentity", userIdentity.Identity)
            .WithParameter("$uploaderIdentityType", userIdentity.IdentityType)
            .Execute();
    }


    private List<int> GetFileUploadsToDelete(
        WorkspaceContext workspace,
        FileUploadExtId[] fileUploadExternalIds,
        IUserIdentity userIdentity,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        if (fileUploadExternalIds.Length == 0)
            return [];

        return dbWriteContext
            .Cmd(
                sql: @"  
                    SELECT fu_id
                    FROM fu_file_uploads
                    WHERE                         
                        fu_workspace_id = $workspaceId
                        AND fu_external_id IN (
                            SELECT value FROM json_each($fileUploadExternalIds)
                        )
                        AND fu_owner_identity_type = $uploaderIdentityType
                        AND fu_owner_identity = $uploaderIdentity
                ",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$workspaceId", workspace.Id)
            .WithJsonParameter("$fileUploadExternalIds", fileUploadExternalIds)
            .WithParameter("$uploaderIdentity", userIdentity.Identity)
            .WithParameter("$uploaderIdentityType", userIdentity.IdentityType)
            .Execute();
    }

    private FoldersMarkedForDelete MarkFoldersForDelete(
        WorkspaceContext workspace,
        FolderExtId[] folderExternalIds,
        int? boxFolderId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        if (folderExternalIds.Length == 0)
            return new FoldersMarkedForDelete(
                FolderIds: [],
                JobsToEnqueue: []);

        var folderIds = dbWriteContext
            .Cmd(
                sql: @"
                    UPDATE fo_folders
                    SET fo_is_being_deleted = TRUE
                    WHERE fo_id IN (
                        WITH folders_to_delete AS (
                            SELECT fo_id
                            FROM fo_folders
                            WHERE
                                fo_workspace_id = $workspaceId
                                AND fo_is_being_deleted = FALSE
                                AND fo_external_id IN (
                                    SELECT value FROM json_each($folderExternalIds)
                                )
                                AND (
                                    $boxFolderId IS NULL 
                                    OR $boxFolderId = fo_id 
                                    OR $boxFolderId IN (
                                        SELECT value FROM json_each(fo_ancestor_folder_ids)
                                    )
                                )
                        )
                        SELECT DISTINCT fo.fo_id
                        FROM fo_folders AS fo, json_each(fo.fo_ancestor_folder_ids) AS ancestor
                        WHERE 
                            fo.fo_is_being_deleted = FALSE
                            AND ancestor.value IN (
                                SELECT fo_id FROM folders_to_delete
                            )
                        UNION 
                        SELECT fo_id FROM folders_to_delete
                    )
                    RETURNING 
                        fo_id
                ",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$boxFolderId", boxFolderId)
            .WithJsonParameter("$folderExternalIds", folderExternalIds)
            .Execute();

        if(folderIds.Count == 0)
            return new FoldersMarkedForDelete(
                FolderIds: [],
                JobsToEnqueue: []);

        var deleteFoldersJob = queue.CreateBulkEntity(
            jobType: DeleteFoldersQueueJobType.Value,
            definition: new DeleteFoldersQueueJobDefinition
            {
                WorkspaceId = workspace.Id,
                DeletedAt = clock.UtcNow,
                FolderIds = folderIds.ToArray()
            },
            sagaId: null);
        
        return new FoldersMarkedForDelete(
            FolderIds: folderIds,
            JobsToEnqueue: [deleteFoldersJob]);
    }
    
    private readonly record struct FoldersMarkedForDelete(
        List<int> FolderIds,
        List<BulkQueueJobEntity> JobsToEnqueue);

}