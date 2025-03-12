using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;
using PlikShare.Folders.Id;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Files.BulkDownload;

public class GetBulkDownloadDetailsQuery(PlikShareDb plikShareDb)
{
    public Result Execute(
        WorkspaceContext workspace,
        List<FolderExtId> selectedFolderExternalIds,
        List<FolderExtId> excludedFolderExternalIds,
        List<FileExtId> selectedFileExternalIds,
        List<FileExtId> excludedFileExternalIds,
        int? boxFolderId)
    {
        using var connection = plikShareDb.OpenConnection();

        var (selectedFolders, excludedFolders) = GetRequestFolders(
            workspace: workspace, 
            selectedFolderExternalIds: selectedFolderExternalIds, 
            excludedFolderExternalIds: excludedFolderExternalIds, 
            boxFolderId: boxFolderId, 
            connection: connection);

        var (selectedFiles, excludedFiles) = GetRequestFiles(
            workspace, 
            selectedFileExternalIds, 
            excludedFileExternalIds, 
            boxFolderId, 
            connection);

        return new Result
        {
            ExcludedFiles = excludedFiles,
            ExcludedFolders = excludedFolders,
            SelectedFiles = selectedFiles,
            SelectedFolders = selectedFolders
        };
    }

    private static (List<Folder> SelectedFolders, List<Folder> ExcludedFolders) GetRequestFolders(
        WorkspaceContext workspace,
        List<FolderExtId> selectedFolderExternalIds,
        List<FolderExtId> excludedFolderExternalIds,
        int? boxFolderId,
        SqliteConnection connection)
    {
        if (selectedFolderExternalIds.Count == 0 && excludedFolderExternalIds.Count == 0)
        {
            return ([], []);
        }

        var requestFolders = connection
            .Cmd(
                sql: @"
                    SELECT
                        fo_id,
                        fo_external_id
                    FROM fo_folders
                    WHERE 
                        fo_is_being_deleted = FALSE
                        AND fo_workspace_id = $workspaceId
                        AND fo_external_id IN (
                            SELECT value FROM json_each($selectedFolderExternalIds)
                            UNION ALL
                            SELECT value FROM json_each($excludedFolderExternalIds)                            
                        )                        
                        AND (
                            $boxFolderId IS NULL
                            OR $boxFolderId = fo_id
                            OR $boxFolderId IN (
                                SELECT value FROM json_each(fo_ancestor_folder_ids)
                            )
                        )
                ",
                readRowFunc: reader => new Folder(
                    Id: reader.GetInt32(0),
                    ExternalId: reader.GetExtId<FolderExtId>(1)))
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$boxFolderId", boxFolderId)
            .WithJsonParameter("$selectedFolderExternalIds", selectedFolderExternalIds)
            .WithJsonParameter("$excludedFolderExternalIds", excludedFolderExternalIds)
            .Execute();

        var selectedFolders = requestFolders
            .Where(folder => selectedFolderExternalIds.Any(fId => fId == folder.ExternalId))
            .ToList();

        var excludedFolders = requestFolders
            .Where(folder => excludedFolderExternalIds.Any(fId => fId == folder.ExternalId))
            .ToList();

        return (selectedFolders, excludedFolders);
    }

    private static (List<File> SelectedFiles, List<File> ExcludedFiles) GetRequestFiles(
       WorkspaceContext workspace,
       List<FileExtId> selectedFileExternalIds,
       List<FileExtId> excludedFileExternalIds,
       int? boxFolderId,
       SqliteConnection connection)
    {
        if (selectedFileExternalIds.Count == 0 && excludedFileExternalIds.Count == 0)
        {
            return ([], []);
        }

        var requestedFiles = connection
            .Cmd(
                sql: @"
                    SELECT
                        fi_id,
                        fi_external_id
                    FROM fi_files
                    LEFT JOIN fo_folders
                        ON fo_id = fi_folder_id
                    WHERE
                        fi_workspace_id = $workspaceId
                        AND fi_external_id IN (
                            SELECT value FROM json_each($selectedFileExternalIds)
                            UNION ALL
                            SELECT value FROM json_each($excludedFileExternalIds)  
                        )
                        AND ((
                                fo_id IS NULL 
                                AND $boxFolderId IS NULL
                            ) OR (
                                fo_id IS NOT NULL
                                AND (
                                    $boxFolderId IS NULL
                                    OR $boxFolderId = fo_id
                                    OR $boxFolderId IN (
                                        SELECT value FROM json_each(fo_ancestor_folder_ids)
                                    )
                                )
                            )
                        )
                ",
                readRowFunc: reader => new File(
                    Id: reader.GetInt32(0),
                    ExternalId:reader.GetExtId<FileExtId>(1)))
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$boxFolderId", boxFolderId)
            .WithJsonParameter("$selectedFileExternalIds", selectedFileExternalIds)
            .WithJsonParameter("$excludedFileExternalIds", excludedFileExternalIds)
            .Execute();

        var selectedFiles = requestedFiles
            .Where(folder => selectedFileExternalIds.Any(fId => fId == folder.ExternalId))
            .ToList();

        var excludedFiles = requestedFiles
            .Where(folder => excludedFileExternalIds.Any(fId => fId == folder.ExternalId))
            .ToList();

        return (selectedFiles, excludedFiles);
    }
    
    public class Result
    {
        public required List<Folder> SelectedFolders { get; init; }
        public required List<Folder> ExcludedFolders { get; init; }
        public required List<File> SelectedFiles { get; init; }
        public required List<File> ExcludedFiles { get; init; }
    }

    public record File(
        int Id,
        FileExtId ExternalId);

    public record Folder(
        int Id,
        FolderExtId ExternalId);
}