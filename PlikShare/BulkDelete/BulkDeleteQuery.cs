using Microsoft.Data.Sqlite;
using PlikShare.BulkDelete.Contracts;
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
using PlikShare.Trash;
using PlikShare.Uploads.Delete;
using PlikShare.Uploads.Id;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.GetSize;
using PlikShare.Workspaces.UpdateCurrentSizeInBytes.QueueJob;
using Serilog;
using Serilog.Events;

namespace PlikShare.BulkDelete;

public class BulkDeleteQuery(
    IClock clock,
    IQueue queue,
    DbWriteQueue dbWriteQueue,
    DeleteFilesSubQuery deleteFilesSubQuery,
    SoftDeleteFilesSubQuery softDeleteFilesSubQuery,
    DeleteFileUploadsSubQuery deleteFileUploadsSubQuery,
    DeleteCopyFileQueueJobsSubQuery deleteCopyFileQueueJobsSubQuery,
    DeleteTextractJobsSubQuery deleteTextractJobsSubQuery,
    GetWorkspaceSizeQuery getWorkspaceSizeQuery)
{
    public async Task<BulkDeleteResponseDto> Execute(
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
        await dbWriteQueue.Execute(
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

        if(boxFolderId is null)
        {
            var workspaceSize = getWorkspaceSizeQuery.Execute(
                workspace);

            return new BulkDeleteResponseDto
            {
                NewWorkspaceSizeInBytes = workspaceSize
            };
        }

        return new BulkDeleteResponseDto
        {
            NewWorkspaceSizeInBytes = null
        };
    }


    private void ExecuteOperation(
        SqliteWriteContext dbWriteContext,
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

            // When trash policy is enabled (and the workspace itself isn't being torn down),
            // soft-delete the files instead of physical-deleting them. The workspace IsBeingDeleted
            // bypass exists because ScheduleWorkspaceDelete already commits to wiping everything;
            // routing through trash would just delay the same outcome and waste storage.
            var trashEnabled = workspace.TrashPolicy.Enabled && !workspace.IsBeingDeleted;

            var filesToDelete = GetFilesToDelete(
                workspace,
                fileExternalIds,
                boxFolderId,
                userIdentity,
                isFileDeleteAllowedByBoxPermissions,
                dbWriteContext,
                transaction);

            List<DeleteFilesSubQuery.DeletedFile> deletedFiles;
            int[] deletedFileIds;

            if (trashEnabled)
            {
                var softResult = softDeleteFilesSubQuery.Execute(
                    workspaceId: workspace.Id,
                    fileIds: filesToDelete,
                    deletedAt: clock.UtcNow,
                    dbWriteContext: dbWriteContext,
                    transaction: transaction);

                // Soft-deleted files keep their storage objects; downstream cleanup steps
                // (textract / copy-file) skip them too (deletedFileIds stays empty), and their
                // pickup queries are gated on fi_deleted_at IS NULL so pending work just stalls
                // until the file is restored or the sweeper purges it.
                deletedFiles = [];
                deletedFileIds = [];

                if (Log.IsEnabled(LogEventLevel.Information))
                {
                    Log.Information(
                        "Soft-deleted {Count} files in Workspace#{WorkspaceId} (trash policy: {Policy})",
                        softResult.SoftDeletedFiles.Count,
                        workspace.Id,
                        Json.Serialize(workspace.TrashPolicy));
                }
            }
            else
            {
                var (hardDeletedFiles, fileJobs) = deleteFilesSubQuery.Execute(
                    workspaceId: workspace.Id,
                    fileIds: filesToDelete,
                    sagaId: null,
                    dbWriteContext: dbWriteContext,
                    transaction: transaction);

                jobsToEnqueue.AddRange(fileJobs);
                deletedFiles = hardDeletedFiles;
                deletedFileIds = deletedFiles.Select(df => df.Id).ToArray();
            }

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

            // Trash-mode folder delete: pre-soft-delete every still-live file in the marked
            // subtree (snapshotting each file's path with its true parent folder) BEFORE the
            // physical folder-delete queue job runs. After this, those files have
            // fi_folder_id = NULL, so BulkDeleteFoldersWithDependenciesQuery.GetFilesToDelete
            // returns zero rows for them and the folder rows can be hard-deleted alone.
            if (trashEnabled && foldersMarkedAsBeingDeleted.Count > 0)
            {
                var filesInSubtree = GetLiveFilesInFolders(
                    workspaceId: workspace.Id,
                    folderIds: foldersMarkedAsBeingDeleted,
                    dbWriteContext: dbWriteContext,
                    transaction: transaction);

                if (filesInSubtree.Count > 0)
                {
                    softDeleteFilesSubQuery.Execute(
                        workspaceId: workspace.Id,
                        fileIds: filesInSubtree,
                        deletedAt: clock.UtcNow,
                        dbWriteContext: dbWriteContext,
                        transaction: transaction);
                }
            }

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

            var updateWorkspaceCurrentSizeJob = queue.EnqueueWorkspaceSizeUpdateJob(
                clock: clock,
                workspaceId: workspace.Id,
                correlationId: correlationId,
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
        SqliteWriteContext dbWriteContext,
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
        SqliteWriteContext dbWriteContext,
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
        SqliteWriteContext dbWriteContext,
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
            sagaId: null,
            batchId: null);
        
        return new FoldersMarkedForDelete(
            FolderIds: folderIds,
            JobsToEnqueue: [deleteFoldersJob]);
    }
    
    private readonly record struct FoldersMarkedForDelete(
        List<int> FolderIds,
        List<BulkQueueJobEntity> JobsToEnqueue);

    private static List<int> GetLiveFilesInFolders(
        int workspaceId,
        List<int> folderIds,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        if (folderIds.Count == 0)
            return [];

        return dbWriteContext
            .Cmd(
                sql: @"
                    SELECT fi_id
                    FROM fi_files
                    WHERE fi_workspace_id = $workspaceId
                      AND fi_folder_id IN (SELECT value FROM json_each($folderIds))
                      AND fi_deleted_at IS NULL
                ",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .WithJsonParameter("$folderIds", folderIds)
            .Execute();
    }
}