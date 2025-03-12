using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Folders.Id;
using PlikShare.Uploads.Id;
using PlikShare.Workspaces.Cache;
using Serilog;
using Serilog.Events;

namespace PlikShare.Folders.MoveToFolder;

public class MoveItemsToFolderQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        WorkspaceContext workspace,
        FolderExtId[] folderExternalIds,
        FileExtId[] fileExternalIds,
        FileUploadExtId[] fileUploadExternalIds,
        FolderExtId? destinationFolderExternalId,
        int? boxFolderId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context, 
                workspace: workspace, 
                folderExternalIds: folderExternalIds, 
                fileExternalIds: fileExternalIds, 
                fileUploadExternalIds: fileUploadExternalIds,
                destinationFolderExternalId: destinationFolderExternalId, 
                boxFolderId: boxFolderId),
            cancellationToken: cancellationToken);
    }


    private ResultCode ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        WorkspaceContext workspace,
        FolderExtId[] folderExternalIds,
        FileExtId[] fileExternalIds,
        FileUploadExtId[] fileUploadExternalIds,
        FolderExtId? destinationFolderExternalId,
        int? boxFolderId)
    {

        using var transaction = dbWriteContext.Connection.BeginTransaction();

        if (destinationFolderExternalId is null && boxFolderId is not null)
            throw new ArgumentException(
                $"When {nameof(destinationFolderExternalId)} is null then {nameof(boxFolderId)} cannot have any value but found '{boxFolderId}'");

        try
        {
            var destinationFolder = new DestinationFolder(
                Id: null,
                AncestorFolderIds: []);

            if (destinationFolderExternalId is not null)
            {
                var destinationFolderResult = GetDestinationFolder(
                    workspace,
                    destinationFolderExternalId.Value,
                    boxFolderId,
                    dbWriteContext,
                    transaction);

                if (destinationFolderResult.IsEmpty)
                {
                    transaction.Rollback();

                    Log.Warning("Could not move items to destination Folder '{DestinationFolderExternalId}' " +
                                "because destination folder was not found",
                        destinationFolderExternalId);

                    return ResultCode.DestinationFolderNotFound;
                }

                destinationFolder = destinationFolderResult.Value;
            }

            var movedFiles = MoveFilesToDestinationFolder(
                destinationFolder.Id,
                workspace,
                fileExternalIds,
                boxFolderId,
                dbWriteContext,
                transaction);

            if (movedFiles.Count != fileExternalIds.Length)
            {
                transaction.Rollback();

                Log.Warning(
                    "Could not move Files: {ExpectedFileExternalIds} to destination Folder '{DestinationFolderExternalId}' " +
                    "because only some of them were found. Query result: '{@QueryResult}'",
                    fileExternalIds,
                    destinationFolderExternalId,
                    movedFiles);

                return ResultCode.FilesNotFound;
            }

            var movedFileUploads = MoveUploadsToDestinationFolder(
                destinationFolder.Id,
                workspace,
                fileUploadExternalIds,
                boxFolderId,
                dbWriteContext,
                transaction);

            if (movedFileUploads.Count != fileUploadExternalIds.Length)
            {
                transaction.Rollback();

                Log.Warning(
                    "Could not move FileUploads: {ExpectedFileUploadExternalIds} to destination Folder '{DestinationFolderExternalId}'. " +
                    "Query result: '{@QueryResult}'",
                    fileUploadExternalIds,
                    destinationFolderExternalId,
                    movedFileUploads);

                return ResultCode.UploadsNotFound;
            }

            var foldersToMove = GetFoldersToMove(
                workspace,
                folderExternalIds,
                boxFolderId,
                dbWriteContext,
                transaction);

            if (foldersToMove.Count < folderExternalIds.Length)
            {
                transaction.Rollback();

                Log.Warning(
                    "Could not move Folders: {ExpectedFolderExternalIds} to destination folder '{DestinationFolderExternalId}' " +
                    "because only some of them were found. Query result: '{@QueryResult}'",
                    folderExternalIds,
                    destinationFolderExternalId,
                    foldersToMove);

                return ResultCode.FoldersNotFound;
            }

            var allMovedFolders = new List<MovedFolder>();

            foreach (var folderToMove in foldersToMove)
            {
                var movedFolders = MoveFolders(
                    folderToMove,
                    destinationFolder,
                    workspace,
                    dbWriteContext,
                    transaction);

                allMovedFolders.AddRange(movedFolders);
            }

            if (allMovedFolders.Any(folder => folder.WasMovedToOwnSubfolder))
            {
                transaction.Rollback();

                Log.Warning(
                    "Could not move Folders: {ExpectedFolderExternalIds} to destination folder '{DestinationFolderExternalId}' " +
                    "because some of them would be moved to their own subfolder. Query result: '{@QueryResult}'",
                    folderExternalIds,
                    destinationFolderExternalId,
                    allMovedFolders);

                return ResultCode.FoldersMovedToOwnSubfolder;
            }

            transaction.Commit();

            if (Log.IsEnabled(LogEventLevel.Information))
            {
                var fileIds = IdsRange.GroupConsecutiveIds(
                    ids: movedFiles);

                var fileUploadIds = IdsRange.GroupConsecutiveIds(
                    ids: movedFileUploads);

                var folderIds = IdsRange.GroupConsecutiveIds(
                    ids: allMovedFolders.Select(x => x.Id));

                Log.Information(
                    "Items were moved to Folder '{DestinationFolderExternalId}'. " +
                    "Files ({FilesCount}): [{FileIds}], " +
                    "FileUploads ({FileUploadsCount}): [{FileUploadIds}], " +
                    "Folders ({FoldersCount}): [{FolderIds}]",
                    destinationFolderExternalId,
                    movedFiles.Count,
                    fileIds,
                    movedFileUploads.Count,
                    fileUploadIds,
                    allMovedFolders.Count,
                    folderIds);
            }

            return ResultCode.Ok;
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while moving " +
                         "Folders: '{FolderExternalIds}' " +
                         "Files: '{FileExternalIds}' " +
                         "Uploads: '{UploadExternalIds}' " +
                         "to Destination folder: '{DestinationFolderExternalId}' " +
                         "(BoxFolderId: '{BoxFolderId}'",
                folderExternalIds,
                fileExternalIds,
                fileUploadExternalIds,
                destinationFolderExternalId,
                boxFolderId);

            throw;
        }
    }

    private static SQLiteOneRowCommandResult<DestinationFolder> GetDestinationFolder(
        WorkspaceContext workspace, 
        FolderExtId destinationFolderExternalId, 
        int? boxFolderId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        return dbWriteContext
            .OneRowCmd(
                sql: @"
                        SELECT
                            fo_id,
                            fo_ancestor_folder_ids
                        FROM fo_folders
                        WHERE
                            fo_external_id = $destinationFolderExternalId
                            AND fo_workspace_id = $workspaceId
                            AND fo_is_being_deleted = FALSE
                            AND (
                                $boxFolderId IS NULL
                                OR $boxFolderId = fo_id
                                OR $boxFolderId IN (
                                    SELECT value FROM json_each(fo_ancestor_folder_ids)
                                )
                            )
                    ",
                readRowFunc: reader => new DestinationFolder(
                    Id: reader.GetInt32(0),
                    AncestorFolderIds: reader.GetFromJson<int[]>(1)),
                transaction: transaction)
            .WithParameter("$destinationFolderExternalId", destinationFolderExternalId.Value)
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$boxFolderId", boxFolderId)
            .Execute();
    }
    
    private static List<int> MoveFilesToDestinationFolder(
        int? destinationFolderId,
        WorkspaceContext workspace, 
        FileExtId[] fileExternalIds, 
        int? boxFolderId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        if (fileExternalIds.Length == 0)
            return [];

        return dbWriteContext
            .Cmd(
                sql: @"
                        UPDATE fi_files
                        SET fi_folder_id = $destinationFolderId
                        WHERE
                            fi_external_id IN (
                                SELECT value FROM json_each($fileExternalIds)
                            )
                            AND fi_workspace_id = $workspaceId
                            AND (
                                $boxFolderId IS NULL
                                OR EXISTS (
                                    SELECT 1
                                    FROM fo_folders
                                    WHERE 
                                        fo_id = fi_folder_id
                                        AND fo_workspace_id = $workspaceId
                                        AND fo_is_being_deleted = FALSE
                                        AND (
                                            $boxFolderId = fo_id
                                            OR $boxFolderId IN (
                                                SELECT value FROM json_each(fo_ancestor_folder_ids)
                                            )
                                        )
                                )
                            )
                        RETURNING
                            fi_id                            
                    ",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$destinationFolderId", destinationFolderId)
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$boxFolderId", boxFolderId)
            .WithJsonParameter("$fileExternalIds", fileExternalIds)
            .Execute();
    }

    private static List<int> MoveUploadsToDestinationFolder(
        int? destinationFolderId,
        WorkspaceContext workspace,
        FileUploadExtId[] fileUploadExternalIds,
        int? boxFolderId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        if (fileUploadExternalIds.Length == 0)
            return [];

        return dbWriteContext
            .Cmd(
                sql: @"
                        UPDATE fu_file_uploads
                        SET fu_folder_id = $destinationFolderId
                        WHERE
                            fu_external_id IN (
                                SELECT value FROM json_each($fileUploadExternalIds)
                            )
                            AND fu_workspace_id = $workspaceId
                            AND (
                                $boxFolderId IS NULL
                                OR EXISTS (
                                    SELECT 1
                                    FROM fo_folders
                                    WHERE 
                                        fo_id = fu_folder_id
                                        AND fo_workspace_id = $workspaceId
                                        AND fo_is_being_deleted = FALSE
                                        AND (
                                            $boxFolderId = fo_id
                                            OR $boxFolderId IN (
                                                SELECT value FROM json_each(fo_ancestor_folder_ids)
                                            )
                                        )
                                )
                            )
                        RETURNING
                            fu_id                            
                    ",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$destinationFolderId", destinationFolderId)
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$boxFolderId", boxFolderId)
            .WithJsonParameter("$fileUploadExternalIds", fileUploadExternalIds)
            .Execute();
    }
    
    
    private static List<FolderToMove> GetFoldersToMove(
        WorkspaceContext workspace, 
        FolderExtId[] folderExternalIds, 
        int? boxFolderId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        if (folderExternalIds.Length == 0)
            return [];

        return dbWriteContext
            .Cmd(
                sql: @"
                        SELECT
                            fo_id,
                            fo_ancestor_folder_ids
                        FROM fo_folders
                        WHERE
                            fo_external_id IN (
                                SELECT value FROM json_each($folderExternalIds)
                            )
                            AND fo_workspace_id = $workspaceId
                            AND (
                                $boxFolderId IS NULL
                                OR $boxFolderId IN (
                                    SELECT value FROM json_each(fo_ancestor_folder_ids)
                                )
                            )
                    ",
                readRowFunc: reader => new FolderToMove(
                    Id: reader.GetInt32(0),
                    AncestorFolderIds: reader.GetFromJson<int[]>(1)),
                transaction: transaction)
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$boxFolderId", boxFolderId)
            .WithJsonParameter("$folderExternalIds", folderExternalIds)
            .Execute();
    }
    
    
    private static List<MovedFolder> MoveFolders( 
        FolderToMove folderToMove, 
        DestinationFolder destinationFolder,
        WorkspaceContext workspace,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        return dbWriteContext
            .Cmd(
                sql: @"
                    UPDATE fo_folders
                    SET
                        fo_parent_folder_id = (CASE
                            WHEN fo_id = $folderToMoveId THEN $destinationFolderId
                            ELSE fo_parent_folder_id
                        END),
                        fo_ancestor_folder_ids = (
                            SELECT json_group_array(value)
                            FROM (
                                SELECT value 
                                FROM json_each($destinationFolderPath)
                                UNION ALL 
                                SELECT value 
                                FROM json_each(fo_ancestor_folder_ids)
                                WHERE json_each.key >= $folderToMovePathLength
                            )
                        )
                    WHERE
                        fo_workspace_id = $workspaceId
                        AND (
                            $folderToMoveId = fo_id
                            OR $folderToMoveId IN (
                                SELECT value FROM json_each(fo_ancestor_folder_ids)
                            )
                        )
                    RETURNING
                        fo_id,
                        fo_ancestor_folder_ids
                    ",
                readRowFunc: reader => new MovedFolder(
                    Id: reader.GetInt32(0),
                    AncestorFolderIds: reader.GetFromJson<int[]>(1)),
                transaction: transaction)
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$folderToMoveId", folderToMove.Id)
            .WithParameter("$destinationFolderId", destinationFolder.Id)
            .WithJsonParameter("$destinationFolderPath", destinationFolder.Path)
            .WithParameter("$folderToMovePathLength", folderToMove.AncestorFolderIds.Length)
            .Execute();
    }

    public enum ResultCode
    {
        Ok,
        DestinationFolderNotFound,
        FilesNotFound,
        UploadsNotFound,
        FoldersNotFound,
        FoldersMovedToOwnSubfolder
    }

    private readonly record struct DestinationFolder(
        int? Id,
        int[] AncestorFolderIds)
    {
        public int[] Path { get; } = Id is null ? [] : [..AncestorFolderIds, Id.Value];
    }

    
    private readonly record struct FolderToMove(
        int Id,
        int[] AncestorFolderIds);

    private readonly record struct MovedFolder(
        int Id,
        int[] AncestorFolderIds)
    {
        public bool WasMovedToOwnSubfolder => AncestorFolderIds.Contains(Id);
    }
}