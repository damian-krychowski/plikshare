using Microsoft.Data.Sqlite;
using PlikShare.Boxes.Id;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.Delete;
using PlikShare.Integrations.Aws.Textract.Jobs.Delete;
using PlikShare.Storages.FileCopying.Delete;
using PlikShare.Uploads.Delete;
using PlikShare.Workspaces.UpdateCurrentSizeInBytes.QueueJob;
using Serilog;
using Serilog.Events;

namespace PlikShare.Folders.Delete.QueueJob;

public class BulkDeleteFoldersWithDependenciesQuery(
    IClock clock,
    IQueue queue,
    DeleteFilesSubQuery deleteFilesSubQuery,
    DeleteFileUploadsSubQuery deleteFileUploadsSubQuery,
    DeleteCopyFileQueueJobsSubQuery deleteCopyFileQueueJobsSubQuery,
    DeleteTextractJobsSubQuery deleteTextractJobsSubQuery)
{
    public Result Execute(
        int workspaceId,
        int[] folderIds,
        DateTimeOffset deletedAt,
        Guid correlationId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        var workspace = dbWriteContext
            .OneRowCmd(
                sql: @"
                    SELECT w_id
                    FROM w_workspaces
                    WHERE w_id = $workspaceId",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .Execute();

        if (workspace.IsEmpty)
        {
            Log.Warning("Workspace#{WorkspaceId} was not found, batch delete folders operation will be cancelled.",
                workspaceId);

            return new Result(DetachedBoxes: []);
        }
        
        dbWriteContext.DeferForeignKeys(
            transaction: transaction);

        var jobsToEnqueue = new List<BulkQueueJobEntity>();

        var allFolderToDeleteIds = GetAllFolderIds(
            workspaceId, 
            folderIds, 
            dbWriteContext, 
            transaction);
        
        var filesToDelete = GetFilesToDelete(
            workspaceId,
            allFolderToDeleteIds,
            dbWriteContext,
            transaction);

        var (deletedFiles, filesJobs) = deleteFilesSubQuery.Execute(
            workspaceId: workspaceId,
            fileIds: filesToDelete,
            sagaId: null,
            dbWriteContext: dbWriteContext,
            transaction: transaction);

        jobsToEnqueue.AddRange(filesJobs);

        var deletedFileIds = deletedFiles
            .Select(df => df.Id)
            .ToArray();
        
        var fileUploadsToDelete = GetFileUploadsToDelete(
            workspaceId,
            allFolderToDeleteIds,
            dbWriteContext,
            transaction);

        var (deletedFileUploads, deletedFileUploadParts, fileUploadJobs) = deleteFileUploadsSubQuery.Execute(
            fileUploadIds: fileUploadsToDelete,
            sagaId: null,
            dbWriteContext: dbWriteContext,
            transaction: transaction);

        jobsToEnqueue.AddRange(fileUploadJobs);

        var (deletedCopyFileQueueJobs, deletedCopyFileUploads, deletedCopyFileUploadParts, copyFileUploadJobs) =
            deleteCopyFileQueueJobsSubQuery.Execute(
                workspaceId: workspaceId,
                deletedFileIds: deletedFileIds,
                deletedFileUploadIds: deletedFileUploads.Select(dfu => dfu.Id).ToArray(),
                dbWriteContext: dbWriteContext,
                transaction: transaction);
        
        jobsToEnqueue.AddRange(copyFileUploadJobs);
        
        var deletedTextractJobs = deleteTextractJobsSubQuery.Execute(
            workspaceId: workspaceId,
            deletedFileIds: deletedFileIds,
            dbWriteContext: dbWriteContext,
            transaction: transaction);

        var detachedBoxes = DetachBoxesFromFolders(
            workspaceId,
            allFolderToDeleteIds, 
            dbWriteContext,
            transaction);

        var deletedFolders = DeleteFolders(
            workspaceId,
            allFolderToDeleteIds,
            dbWriteContext,
            transaction);

        var queueJobs = queue.EnqueueBulk(
            correlationId: correlationId,
            definitions: jobsToEnqueue,
            executeAfterDate: clock.UtcNow,
            dbWriteContext: dbWriteContext,
            transaction: transaction);

        var updateWorkspaceCurrentSizeJob = queue.EnqueueWorkspaceSizeUpdateJob(
            clock: clock,
            workspaceId: workspaceId,
            correlationId: correlationId,
            dbWriteContext: dbWriteContext,
            transaction: transaction);

        queueJobs.Add(updateWorkspaceCurrentSizeJob);

        if (Log.IsEnabled(LogEventLevel.Information))
        {
            var fileIds = IdsRange.GroupConsecutiveIds(
                ids: deletedFiles.Select(x => x.Id));

            var fileUploadIds = IdsRange.GroupConsecutiveIds(
                ids: deletedFileUploads.Select(x => x.Id).Concat(deletedCopyFileUploads.Select(x => x.Id)));

            var allFolderIds = IdsRange.GroupConsecutiveIds(
                ids: deletedFolders);

            var queueJobIds = IdsRange.GroupConsecutiveIds(
                ids: queueJobs.Select(x => x.Value));

            var copyFileQueueJobIds = IdsRange.GroupConsecutiveIds(
                ids: deletedCopyFileQueueJobs.Select(x => x.Id));

            var textractJobIds = IdsRange.GroupConsecutiveIds(
                ids: deletedTextractJobs.Select(x => x.Id));

            Log.Information(
                "Bulk delete folders query finished. " +
                "Deleted Folders ({FoldersCount}): {FolderIds}, " +
                "Files ({FilesCount}): [{FileIds}], " +
                "FileUploads ({FileUploadsCount}): [{FileUploadIds}], " +
                "FileUploadParts ({FileUploadPartsCount}). " +
                "CopyFileQueueJobs ({CopyFileQueueJobsCount}): [{CopyFileQueueJobIds}]" +
                "TextractJobs ({TextractJobsCount}): [{TextractJobIds}]" +
                "Enqueued jobs ({QueueJobsCount}): [{QueueJobIds}]",
                deletedFolders.Count,
                allFolderIds,
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

        return new Result(
            DetachedBoxes: detachedBoxes.Select(box => box.ExternalId).ToArray());
    }
    
    private static int[] GetAllFolderIds(
        int workspaceId,
        int[] folderIds,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        if (folderIds.Length == 0)
            return [];

        var result = dbWriteContext
            .Cmd(
                sql: @"
                    SELECT fo_id
                    FROM fo_folders, json_each(fo_folders.fo_ancestor_folder_ids)
                    WHERE
                        fo_workspace_id = $workspaceId
                        AND json_each.value IN (
                            SELECT value FROM json_each($folderIds)
                        )
                ",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .WithJsonParameter("$folderIds", folderIds)
            .Execute();

        return folderIds
            .Concat(result)
            .Distinct()
            .ToArray();
    }

    private static List<int> DeleteFolders(
        int workspaceId,
        int[] folderIds,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        if (folderIds.Length == 0)
            return [];

        return dbWriteContext
            .Cmd(
                sql: @"
                    DELETE FROM fo_folders
                    WHERE 
                        fo_workspace_id = $workspaceId
                        AND fo_id IN (
                             SELECT value FROM json_each($folderIds)
                        )                 
                    RETURNING 
                        fo_id
                ",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .WithJsonParameter("$folderIds", folderIds)
            .Execute();
    }
    
    private static List<DetachedBox> DetachBoxesFromFolders(
        int workspaceId,
        int[] folderIds,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        return dbWriteContext
            .Cmd(
                sql: @"
                    UPDATE bo_boxes
                    SET bo_folder_id = NULL
                    WHERE 
                        bo_workspace_id = $workspaceId
                        AND bo_folder_id IN (
                            SELECT value FROM json_each($folderIds)
                        )
                    RETURNING 
                        bo_external_id
                ",
                readRowFunc: reader => new DetachedBox(
                    ExternalId: reader.GetExtId<BoxExtId>(0)),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .WithJsonParameter("$folderIds", folderIds)
            .Execute();
    }

    private static List<int> GetFileUploadsToDelete(
        int workspaceId,
        int[] folderIds,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        if (folderIds.Length == 0)
            return [];

        return dbWriteContext
            .Cmd(
                sql: @"  
                    SELECT fu_id 
                    FROM fu_file_uploads
                    WHERE 
                        fu_workspace_id = $workspaceId
                        AND fu_folder_id IN (
                            SELECT value FROM json_each($folderIds)
                        )
                ",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .WithJsonParameter("$folderIds", folderIds)
            .Execute();
    }


    private static List<int> GetFilesToDelete(
        int workspaceId,
        int[] folderIds,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        if (folderIds.Length == 0)
            return [];

        return dbWriteContext
            .Cmd(
                sql: @"
                    SELECT fi_id 
                    FROM fi_files
                    WHERE 
                        fi_workspace_id = $workspaceId
                        AND fi_folder_id IN (
                            SELECT value FROM json_each($folderIds)
                        )              
                ",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .WithJsonParameter("$folderIds", folderIds)
            .Execute();
    }

    public record Result(
        BoxExtId[] DetachedBoxes);

    private readonly record struct DetachedBox(
        BoxExtId ExternalId);
}