using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Workspaces.Cache;

namespace PlikShare.MediaProcessing.Generation;

// Resolves an include/exclude tree selection (selected/excluded folder + file external ids) into a
// flat list of candidate file external ids: directly selected files plus every file recursively
// under a selected folder, minus excluded subtrees and minus excluded files. Thumbnailability is NOT
// filtered here (extension is encrypted in Full-encryption workspaces) — the caller's
// GetThumbnailSourceFileQuery.ExecuteBatch drops non-image/video files in memory afterwards.
public class GetThumbnailableSelectionFilesQuery(PlikShareDb plikShareDb)
{
    public List<string> Execute(
        WorkspaceContext workspace,
        List<string> selectedFolders,
        List<string> selectedFiles,
        List<string> excludedFolders,
        List<string> excludedFiles)
    {
        if (selectedFiles.Count == 0 && selectedFolders.Count == 0)
            return [];

        using var connection = plikShareDb.OpenConnection();

        var fileExternalIds = new HashSet<string>();

        if (selectedFiles.Count > 0)
        {
            var directFiles = GetDirectlySelectedFiles(
                workspace: workspace,
                selectedFiles: selectedFiles,
                excludedFiles: excludedFiles,
                connection: connection);

            foreach (var fileExternalId in directFiles)
                fileExternalIds.Add(fileExternalId);
        }

        if (selectedFolders.Count > 0)
        {
            var folderIds = ResolveFolderIds(
                workspace: workspace,
                selectedFolders: selectedFolders,
                excludedFolders: excludedFolders,
                connection: connection);

            if (folderIds.Count > 0)
            {
                var folderFiles = GetFilesInFolders(
                    workspace: workspace,
                    folderIds: folderIds,
                    excludedFiles: excludedFiles,
                    connection: connection);

                foreach (var fileExternalId in folderFiles)
                    fileExternalIds.Add(fileExternalId);
            }
        }

        return fileExternalIds.ToList();
    }

    private static List<string> GetDirectlySelectedFiles(
        WorkspaceContext workspace,
        List<string> selectedFiles,
        List<string> excludedFiles,
        SqliteConnection connection)
    {
        return connection
            .Cmd(
                sql: """
                     SELECT fi_external_id
                     FROM fi_files
                     WHERE
                         fi_workspace_id = $workspaceId
                         AND fi_deleted_at IS NULL
                         AND fi_is_upload_completed = TRUE
                         AND fi_external_id IN (SELECT value FROM json_each($selectedFileExternalIds))
                         AND fi_external_id NOT IN (SELECT value FROM json_each($excludedFileExternalIds))
                     """,
                readRowFunc: reader => reader.GetString(0))
            .WithParameter("$workspaceId", workspace.Id)
            .WithJsonParameter("$selectedFileExternalIds", selectedFiles)
            .WithJsonParameter("$excludedFileExternalIds", excludedFiles)
            .Execute();
    }

    private static List<string> GetFilesInFolders(
        WorkspaceContext workspace,
        List<int> folderIds,
        List<string> excludedFiles,
        SqliteConnection connection)
    {
        return connection
            .Cmd(
                sql: """
                     SELECT fi_external_id
                     FROM fi_files
                     WHERE
                         fi_workspace_id = $workspaceId
                         AND fi_deleted_at IS NULL
                         AND fi_is_upload_completed = TRUE
                         AND fi_folder_id IN (SELECT value FROM json_each($folderIds))
                         AND fi_external_id NOT IN (SELECT value FROM json_each($excludedFileExternalIds))
                     """,
                readRowFunc: reader => reader.GetString(0))
            .WithParameter("$workspaceId", workspace.Id)
            .WithJsonParameter("$folderIds", folderIds)
            .WithJsonParameter("$excludedFileExternalIds", excludedFiles)
            .Execute();
    }

    private static List<int> ResolveFolderIds(
        WorkspaceContext workspace,
        List<string> selectedFolders,
        List<string> excludedFolders,
        SqliteConnection connection)
    {
        var requestFolders = connection
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
                     """,
                readRowFunc: reader => new
                {
                    Id = reader.GetInt32(0),
                    ExternalId = reader.GetString(1)
                })
            .WithParameter("$workspaceId", workspace.Id)
            .WithJsonParameter("$selectedFolderExternalIds", selectedFolders)
            .WithJsonParameter("$excludedFolderExternalIds", excludedFolders)
            .Execute();

        var selectedFolderIds = requestFolders
            .Where(folder => selectedFolders.Contains(folder.ExternalId))
            .Select(folder => folder.Id)
            .ToList();

        if (selectedFolderIds.Count == 0)
            return [];

        var excludedFolderIds = requestFolders
            .Where(folder => excludedFolders.Contains(folder.ExternalId))
            .Select(folder => folder.Id)
            .ToList();

        var descendantFolderIds = connection
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
            .WithJsonParameter("$selectedFolderIds", selectedFolderIds)
            .WithJsonParameter("$excludedFolderIds", excludedFolderIds)
            .Execute();

        // The descendant query only returns folders that have a selected folder among their
        // ancestors — the top selected folders themselves must be added back.
        descendantFolderIds.AddRange(selectedFolderIds);

        return descendantFolderIds;
    }
}
