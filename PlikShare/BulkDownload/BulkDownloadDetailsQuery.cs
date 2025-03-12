using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;

//todo: handle situation when some files or folders from the request
//todo: does not exist in the database
//todo: maybe a warning would be a good start

namespace PlikShare.BulkDownload;

public class BulkDownloadDetailsQuery(PlikShareDb plikShareDb)
{
    public BulkDownloadDetails GetDetailsFromDb(
        int workspaceId,
        int[] selectedFileIds,
        int[] excludedFileIds,
        int[] selectedFolderIds,
        int[] excludedFolderIds)
    {
        using var connection = plikShareDb.OpenConnection();

        var selectedFiles = GetSelectedFiles(
            workspaceId: workspaceId,
            fileIds: selectedFileIds, 
            connection: connection);

        var folders = GetAllFolders(
            workspaceId: workspaceId,
            selectedFolderIds: selectedFolderIds,
            excludedFolderIds: excludedFolderIds,
            connection: connection);

        var nestedFiles = GetFilesFromFolders(
            workspaceId: workspaceId,
            allFolderIds: folders.GetAllIds(), 
            excludedFileIds: excludedFileIds,
            connection: connection);
        
        return new BulkDownloadDetails
        {
            FolderSubtree = folders,
            Files = [..selectedFiles, ..nestedFiles]
        };
    }

    private static List<BulkDownloadFile> GetSelectedFiles(
        int workspaceId,
        int[] fileIds, 
        SqliteConnection connection)
    {
        if(fileIds.Length == 0)
            return [];

        return connection
            .Cmd(
                sql: @"
                    SELECT
                        fi_external_id,
                        fi_name || fi_extension,
                        fi_s3_key_secret_part,
                        fi_size_in_bytes
                    FROM fi_files
                    WHERE 
                        fi_workspace_id = $workspaceId
                        AND fi_id IN (
                            SELECT value FROM json_each($fileIds)
                        )
                ",
                readRowFunc: reader => new BulkDownloadFile
                {
                    ExternalId = reader.GetExtId<FileExtId>(0),
                    FullName = reader.GetString(1),
                    S3KeySecretPart = reader.GetString(2),
                    SizeInBytes = reader.GetInt64(3),
                    FolderId = null
                })
            .WithParameter("$workspaceId", workspaceId)
            .WithJsonParameter("$fileIds", fileIds)
            .Execute();
    }

    private static List<BulkDownloadFile> GetFilesFromFolders(
        int workspaceId,
        int[] allFolderIds,
        int[] excludedFileIds,
        SqliteConnection connection)
    {
        if (allFolderIds.Length == 0)
            return [];

        return connection
            .Cmd(
                sql: @"
                    SELECT
                        fi_external_id,
                        fi_name || fi_extension,
                        fi_s3_key_secret_part,
                        fi_folder_id,
                        fi_size_in_bytes
                    FROM fi_files
                    WHERE 
                        fi_workspace_id = $workspaceId
                        AND fi_folder_id IN (
                            SELECT value FROM json_each($folderIds)
                        )
                        AND fi_id NOT IN (
                            SELECT value FROM json_each($excludedFileIds)
                        )
                ",
                readRowFunc: reader => new BulkDownloadFile
                {
                    ExternalId = reader.GetExtId<FileExtId>(0),
                    FullName = reader.GetString(1),
                    S3KeySecretPart = reader.GetString(2),
                    FolderId = reader.GetInt32(3),
                    SizeInBytes = reader.GetInt64(4)
                })
            .WithParameter("$workspaceId", workspaceId)
            .WithJsonParameter("$folderIds", allFolderIds)
            .WithJsonParameter("$excludedFileIds", excludedFileIds)
            .Execute();
    }

    private static FolderSubtree GetAllFolders(
        int workspaceId,
        int[] selectedFolderIds,
        int[] excludedFolderIds,
        SqliteConnection connection)
    {
        var folders = connection
            .Cmd(
                sql: @" 
                    SELECT
                        fo_id,
                        fo_name,
                        fo_ancestor_folder_ids
                    FROM fo_folders
                    WHERE  
                        fo_workspace_id = $workspaceId
                        AND fo_is_being_deleted = FALSE
                        AND fo_id IN (
                            SELECT value FROM json_each($selectedFolderIds)
                        )
                    UNION ALL                    
                    SELECT
                        fo.fo_id,
                        fo.fo_name,
                        fo.fo_ancestor_folder_ids
                    FROM fo_folders AS fo, json_each(fo_ancestor_folder_ids) AS ancestor
                    WHERE
                        fo_workspace_id = $workspaceId
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
                ",
                readRowFunc: reader => new BulkDownloadFolder
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    AncestorFolderIds = reader.GetFromJson<int[]>(2)
                })
            .WithParameter("$workspaceId", workspaceId)
            .WithJsonParameter("$selectedFolderIds", selectedFolderIds)
            .WithJsonParameter("$excludedFolderIds", excludedFolderIds)
            .Execute();

        return new FolderSubtree(
            selectedFolderIds,
            folders);
    }
}

public class BulkDownloadDetails
{
    public required List<BulkDownloadFile> Files { get; init; }
    public required FolderSubtree FolderSubtree { get; init; }

    public void Deconstruct(out List<BulkDownloadFile> files, out FolderSubtree folderSubtree)
    {
        files = Files;
        folderSubtree = FolderSubtree;
    }
}

public class BulkDownloadFile
{
    public required FileExtId ExternalId {get;init;}
    public required string FullName { get; init; }
    public required string S3KeySecretPart {get;init;}
    public required long SizeInBytes {get;init;}
    public required int? FolderId { get; init; }
}

//todo add unit tests
public class FolderSubtree
{
    private readonly Dictionary<int, BulkDownloadFolder> _foldersDict;
    private readonly HashSet<int> _topFolderIds;
    private readonly Dictionary<int, string> _pathsDict = new ();

    public FolderSubtree(
        int[] topFolderIds,
        List<BulkDownloadFolder> folders)
    {
        _foldersDict = folders.ToDictionary(
            keySelector: f => f.Id,
            elementSelector: f => f);

        _topFolderIds = topFolderIds.ToHashSet();
    }

    public int[] GetAllIds() => _foldersDict.Keys.ToArray();

    public int GetLevelInTree(int? folderId)
    {
        if (folderId is null)
            return 0;

        if (!_foldersDict.TryGetValue(folderId.Value, out var folder))
            throw new ArgumentOutOfRangeException(
                paramName: nameof(folderId),
                message: $"FolderId {folderId} was not found in the bulk download folders subtree");

        if (_topFolderIds.Contains(folder.Id))
            return 0;


        foreach (var topFolderId in _topFolderIds)
        {
            var index = Array.IndexOf(
                array: folder.AncestorFolderIds,
                value: topFolderId);

            if (index >= 0)
            {
                return folder.AncestorFolderIds.Length - index;
            }
        }

        throw new InvalidOperationException(
            $"None top folder was found among ancestors of folder with id: {folderId}");
    }

    public string? GetPath(int? folderId)
    {
        if (folderId is null)
            return null;
        
        if (_pathsDict.TryGetValue(folderId.Value, out var existingPath))
            return existingPath;
        
        var folder = GetFolderOrThrow(folderId.Value);
        var path = string.Empty;

        //faster way to build path if parent path was already built
        if (folder.AncestorFolderIds.Length > 0)
        {
            var parentId = folder
                .AncestorFolderIds
                .Last();

            if (_pathsDict.TryGetValue(parentId, out var parentPath))
            {
                path = parentPath + "/" + folder.Name;

                _pathsDict.Add(folderId.Value, path);
                return path;
            }
        }

        var wasTopDetected = false;
        for (var i = 0; i < folder.AncestorFolderIds.Length; i++)
        {
            var ancestorId = folder.AncestorFolderIds[i];

            if (!wasTopDetected)
            {
                if (_topFolderIds.Contains(ancestorId))
                {
                    wasTopDetected = true;
                    path = GetFolderNameOrThrow(ancestorId);
                }
            }
            else
            {
                path += "/";
                path += GetFolderNameOrThrow(ancestorId);
            }
        }

        path = path == string.Empty
            ? GetFolderNameOrThrow(folderId.Value)
            : path + "/" + GetFolderNameOrThrow(folderId.Value);

        _pathsDict.Add(folderId.Value, path);
        return path;
    }

    private string GetFolderNameOrThrow(int folderId)
    {
        var folder = GetFolderOrThrow(folderId);

        return folder.Name;
    }

    private BulkDownloadFolder GetFolderOrThrow(int folderId)
    {
        if (!_foldersDict.TryGetValue(folderId, out var folder))
            throw new ArgumentOutOfRangeException(
                paramName: nameof(folderId),
                message: $"FolderId {folderId} was not found in the bulk download folders subtree");

        return folder;
    }
}

public class BulkDownloadFolder
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required int[] AncestorFolderIds { get; init; }
}