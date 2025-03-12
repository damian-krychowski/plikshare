using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.CountSelectedItems.Contracts;

namespace PlikShare.Workspaces.CountSelectedItems;

public class CountSelectedItemsQuery(PlikShareDb plikShareDb)
{
    public CountSelectedItemsResponseDto Execute(
        WorkspaceContext workspace,
        CountSelectedItemsRequestDto request,
        int? boxFolderId)
    {
        if (request.SelectedFiles.Count == 0 && request.SelectedFolders.Count == 0)
        {
            return new CountSelectedItemsResponseDto
            {
                TotalSizeInBytes = 0,
                SelectedFilesCount = 0,
                SelectedFoldersCount = 0
            };
        }

        using var connection = plikShareDb.OpenConnection();

        var requestFolderIds = GetRequestFolderIds(
            workspace, 
            request, 
            boxFolderId, 
            connection);
        
        var requestedFilesSummary = GetRequestFilesSummary(
            workspace,
            request,
            boxFolderId,
            connection);
        
        var folderIds = GetFolderIds(
            workspace, 
            requestFolderIds, 
            connection);

        var folderFiles = CountFolderFiles(
            workspace, 
            folderIds, 
            connection);
        
        return new CountSelectedItemsResponseDto
        {
            TotalSizeInBytes = folderFiles.TotalSizeInBytesDiff + requestedFilesSummary.TotalSizeInBytesDiff,
            SelectedFilesCount = folderFiles.Count + requestedFilesSummary.CountDiff,
            SelectedFoldersCount = folderIds.Count
        };
    }

    private static FilesSummary CountFolderFiles(
        WorkspaceContext workspace, 
        List<int> folderIds, 
        SqliteConnection connection)
    {
        if (folderIds.Count == 0)
        {
            return new FilesSummary(
                Count: 0,
                TotalSizeInBytesDiff: 0);
        }

        var folderFiles = connection
            .OneRowCmd(
                sql: """
                     SELECT
                         COUNT(fi_id) AS files_count,
                         SUM(fi_size_in_bytes) AS total_size_in_bytes
                     FROM fi_files
                     WHERE 
                         fi_folder_id IN (
                             SELECT value FROM json_each($folderIds)
                         )
                     """,
                readRowFunc: reader => new FilesSummary(
                    Count: reader.GetInt32OrNull(0) ?? 0,
                    TotalSizeInBytesDiff: reader.GetInt64OrNull(1) ?? 0))
            .WithParameter("$workspaceId", workspace.Id)
            .WithJsonParameter("$folderIds", folderIds)
            .ExecuteOrThrow();

        return folderFiles;
    }

    private static List<int> GetFolderIds(
        WorkspaceContext workspace,
        RequestFolderIds requestFolderIds,
        SqliteConnection connection)
    {
        if (requestFolderIds.SelectedFolders.Length == 0 && requestFolderIds.ExcludedFolders.Length == 0)
        {
            return [];
        }

        var folderIds = requestFolderIds.SelectedFolders.Length == 0
            ? []
            : connection
                .Cmd(
                    sql: """
                         SELECT DISTINCT fo_id
                         FROM fo_folders AS fo, json_each(fo.fo_ancestor_folder_ids) AS ancestor
                         WHERE 
                             fo.fo_workspace_id = $workspaceId
                             AND fo.fo_is_being_deleted = FALSE
                             AND ancestor.value IN (
                                 SELECT value FROM json_each($selectedFolderIds)
                             )
                             AND NOT EXISTS (
                                 SELECT 1
                                 FROM json_each($excludedFolderIds)
                                 WHERE value IN (
                                     SELECT fo.fo_id
                                     UNION ALL
                                     SELECT value FROM json_each(fo.fo_ancestor_folder_ids)
                                 )
                             )
                         """,
                    readRowFunc: reader => reader.GetInt32(0))
                .WithParameter("$workspaceId", workspace.Id)
                .WithJsonParameter("$selectedFolderIds", requestFolderIds.SelectedFolders)
                .WithJsonParameter("$excludedFolderIds", requestFolderIds.ExcludedFolders)
                .Execute();

        //we need to include also the top parent folders as they are not being returned
        //from the query above
        folderIds.AddRange(requestFolderIds.SelectedFolders);

        return folderIds;
    }

    private static RequestFolderIds GetRequestFolderIds(
        WorkspaceContext workspace, 
        CountSelectedItemsRequestDto request, 
        int? boxFolderId,
        SqliteConnection connection)
    {
        if (request.SelectedFolders.Count == 0 && request.ExcludedFolders.Count == 0)
        {
            return new RequestFolderIds(
                SelectedFolders: [],
                ExcludedFolders: []);
        }

        var requestFolderIds = connection
            .Cmd(
                sql: """
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
                     """,
                readRowFunc: reader => new
                {
                    Id = reader.GetInt32(0),
                    ExternalId = reader.GetString(1)
                })
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$boxFolderId", boxFolderId)
            .WithJsonParameter("$selectedFolderExternalIds", request.SelectedFolders)
            .WithJsonParameter("$excludedFolderExternalIds", request.ExcludedFolders)
            .Execute();

        var selectedFolderIs = requestFolderIds
            .Where(id => request.SelectedFolders.Any(fId => fId.Value == id.ExternalId))
            .Select(id => id.Id)
            .ToArray();

        var excludedFolderIds = requestFolderIds
            .Where(id => request.ExcludedFolders.Any(fId => fId.Value == id.ExternalId))
            .Select(id => id.Id)
            .ToArray();

        return new RequestFolderIds(
            SelectedFolders: selectedFolderIs,
            ExcludedFolders: excludedFolderIds);
    }

    private static RequestedFilesSummary GetRequestFilesSummary(
        WorkspaceContext workspace,
        CountSelectedItemsRequestDto request,
        int? boxId,
        SqliteConnection connection)
    {
        if (request.SelectedFiles.Count == 0 && request.ExcludedFiles.Count == 0)
        {
            return new RequestedFilesSummary(
                CountDiff: 0,
                TotalSizeInBytesDiff: 0);
        }

        var requestedFiles = connection
            .Cmd(
                sql: """
                     SELECT
                         fi_external_id,
                         fi_size_in_bytes
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
                     """,
                readRowFunc: reader => new
                {
                    ExternalId = reader.GetExtId<FileExtId>(0),
                    SizeInBytes = reader.GetInt64(1)
                })
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$boxFolderId", boxId)
            .WithJsonParameter("$selectedFileExternalIds", request.SelectedFiles)
            .WithJsonParameter("$excludedFileExternalIds", request.ExcludedFiles)
            .Execute();

        var countDiff = 0;
        var totalSizeInBytesDiff = 0L;

        for (var i = 0; i < requestedFiles.Count; i++)
        {
            var file = requestedFiles[i];

            if (request.SelectedFiles.Contains(file.ExternalId))
            {
                countDiff += 1;
                totalSizeInBytesDiff += file.SizeInBytes;
            }
            else
            {
                countDiff -= 1;
                totalSizeInBytesDiff -= file.SizeInBytes;
            }
        }

        return new RequestedFilesSummary(
            CountDiff: countDiff,
            TotalSizeInBytesDiff: totalSizeInBytesDiff);
    }

    private readonly record struct RequestFolderIds(
        int[] SelectedFolders,
        int[] ExcludedFolders);

    private readonly record struct RequestedFilesSummary(
        int CountDiff,
        long TotalSizeInBytesDiff);

    private readonly record struct FilesSummary(
        int Count,
        long TotalSizeInBytesDiff);
}