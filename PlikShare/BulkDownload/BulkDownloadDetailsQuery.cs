using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;
using PlikShare.Storages;

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
        int[] excludedFolderIds,
        IStorageClient storageClient,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        using var connection = plikShareDb.OpenConnection();

        var selectedFiles = GetSelectedFiles(
            workspaceId: workspaceId,
            fileIds: selectedFileIds,
            storageClient: storageClient, 
            workspaceEncryptionSession: workspaceEncryptionSession,
            connection: connection);

        var selectedFileFolderIds = selectedFiles
            .Where(f => f.FolderId is not null)
            .Select(f => f.FolderId!.Value)
            .Distinct()
            .ToArray();

        var folders = GetAllFolders(
            workspaceId: workspaceId,
            selectedFolderIds: selectedFolderIds,
            excludedFolderIds: excludedFolderIds,
            selectedFileFolderIds: selectedFileFolderIds,
            connection: connection);

        var nestedFiles = GetFilesFromFolders(
            workspaceId: workspaceId,
            allFolderIds: folders.GetAllIds(), 
            excludedFileIds: excludedFileIds,
            storageClient: storageClient,
            workspaceEncryptionSession: workspaceEncryptionSession,
            connection: connection);
        
        return new BulkDownloadDetails
        {
            FolderSubtree = folders,
            Files = [..selectedFiles, ..nestedFiles]
        };
    }

    private static List<ResolvedBulkDownloadFile> GetSelectedFiles(
        int workspaceId,
        int[] fileIds,
        IStorageClient storageClient,
        WorkspaceEncryptionSession? workspaceEncryptionSession, 
        SqliteConnection connection)
    {
        if(fileIds.Length == 0)
            return [];

        return connection
            .Cmd(
                sql: """
                     SELECT
                         fi_external_id,
                         fi_name || fi_extension,
                         fi_s3_key_secret_part,
                         fi_size_in_bytes,
                         fi_folder_id,
                         fi_encryption_key_version,
                         fi_encryption_salt,
                         fi_encryption_nonce_prefix,
                         fi_encryption_chain_salts,
                         fi_encryption_format_version
                     FROM fi_files
                     WHERE
                         fi_workspace_id = $workspaceId
                         AND fi_id IN (
                             SELECT value FROM json_each($fileIds)
                         )
                     """,
                readRowFunc: reader =>
                {
                    var encryptionKeyVersion = reader.GetByteOrNull(5);

                    var fileEncryptionMetadata = encryptionKeyVersion is null
                        ? null
                        : new FileEncryptionMetadata
                        {
                            KeyVersion = encryptionKeyVersion.Value,
                            Salt = reader.GetFieldValue<byte[]>(6),
                            NoncePrefix = reader.GetFieldValue<byte[]>(7),
                            ChainStepSalts = KeyDerivationChain.Deserialize(
                                reader.GetFieldValueOrNull<byte[]>(8)),
                            FormatVersion = reader.GetByteOrNull(9) ?? 1
                        };

                    return new ResolvedBulkDownloadFile
                    {
                        ExternalId = reader.GetExtId<FileExtId>(0),
                        FullName = reader.GetString(1),
                        S3KeySecretPart = reader.GetString(2),
                        SizeInBytes = reader.GetInt64(3),
                        FolderId = reader.GetInt32OrNull(4),
                        EncryptionMode = fileEncryptionMetadata.ToEncryptionMode(
                            workspaceEncryptionSession, 
                            storageClient)
                    };
                })
            .WithParameter("$workspaceId", workspaceId)
            .WithJsonParameter("$fileIds", fileIds)
            .Execute();
    }

    private static List<ResolvedBulkDownloadFile> GetFilesFromFolders(
        int workspaceId,
        int[] allFolderIds,
        int[] excludedFileIds,
        IStorageClient storageClient,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        SqliteConnection connection)
    {
        if (allFolderIds.Length == 0)
            return [];

        return connection
            .Cmd(
                sql: """
                     SELECT
                         fi_external_id,
                         fi_name || fi_extension,
                         fi_s3_key_secret_part,
                         fi_folder_id,
                         fi_size_in_bytes,
                         fi_encryption_salt,
                         fi_encryption_nonce_prefix,
                         fi_encryption_chain_salts,
                         fi_encryption_format_version
                     FROM fi_files
                     WHERE 
                         fi_workspace_id = $workspaceId
                         AND fi_folder_id IN (
                             SELECT value FROM json_each($folderIds)
                         )
                         AND fi_id NOT IN (
                             SELECT value FROM json_each($excludedFileIds)
                         )
                     """,
                readRowFunc: reader =>
                {
                    var encryptionKeyVersion = reader.GetByteOrNull(5);

                    var fileEncryptionMetadata = encryptionKeyVersion is null
                        ? null
                        : new FileEncryptionMetadata
                        {
                            KeyVersion = encryptionKeyVersion.Value,
                            Salt = reader.GetFieldValue<byte[]>(6),
                            NoncePrefix = reader.GetFieldValue<byte[]>(7),
                            ChainStepSalts = KeyDerivationChain.Deserialize(
                                reader.GetFieldValueOrNull<byte[]>(8)),
                            FormatVersion = reader.GetByteOrNull(9) ?? 1
                        };

                    return new ResolvedBulkDownloadFile
                    {
                        ExternalId = reader.GetExtId<FileExtId>(0),
                        FullName = reader.GetString(1),
                        S3KeySecretPart = reader.GetString(2),
                        FolderId = reader.GetInt32(3),
                        SizeInBytes = reader.GetInt64(4),
                        EncryptionMode = fileEncryptionMetadata.ToEncryptionMode(
                            workspaceEncryptionSession, 
                            storageClient)
                    };
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
        int[] selectedFileFolderIds,
        SqliteConnection connection)
    {
        // This query fetches four groups of folders in a single shot:
        // 1. The selected (top) folders and their ancestors up to workspace root
        // 2. Folders containing directly-selected files and their ancestors
        //    — both needed to build full paths for audit log
        // 3. Descendants of the selected folders (excluding subtrees under excluded folders)
        //    — these are the folders whose files will be included in the zip
        //
        // After fetching, PrepareFolderSubtree separates them into two dictionaries:
        // - downloadFolders: selected folders + their descendants (used for zip structure)
        // - ancestorFolders: everything else (used only for full path resolution in audit log)
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
                        AND (
                            fo_id IN (SELECT value FROM json_each($selectedFolderIds))
                            OR fo_id IN (
                                SELECT ancestor.value
                                FROM fo_folders AS sf, json_each(sf.fo_ancestor_folder_ids) AS ancestor
                                WHERE sf.fo_id IN (SELECT value FROM json_each($selectedFolderIds))
                            )
                            OR fo_id IN (SELECT value FROM json_each($selectedFileFolderIds))
                            OR fo_id IN (
                                SELECT ancestor.value
                                FROM fo_folders AS ff, json_each(ff.fo_ancestor_folder_ids) AS ancestor
                                WHERE ff.fo_id IN (SELECT value FROM json_each($selectedFileFolderIds))
                            )
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
            .WithJsonParameter("$selectedFileFolderIds", selectedFileFolderIds)
            .Execute();

        return PrepareFolderSubtree(
            selectedFolderIds, 
            folders);
    }

    // Separates the flat list of folders from the query into two groups:
    // - downloadFolders: the selected folders and everything below them (used by zip and GetPath)
    // - ancestorFolders: everything above the selected folders (used only by GetFullPath for audit log)
    // A folder is a "download folder" if it IS a selected folder or has a selected folder among its ancestors.
    // Otherwise it's an ancestor folder (it sits above the selection in the tree).
    private static FolderSubtree PrepareFolderSubtree(
        int[] selectedFolderIds,
        List<BulkDownloadFolder> folders)
    {
        var selectedFolderIdSet = selectedFolderIds.ToHashSet();

        var downloadFolders = new Dictionary<int, BulkDownloadFolder>();
        var ancestorFolders = new Dictionary<int, BulkDownloadFolder>();

        foreach (var folder in folders)
        {
            if (selectedFolderIdSet.Contains(folder.Id)
                || folder.AncestorFolderIds.Any(selectedFolderIdSet.Contains))
            {
                downloadFolders.Add(folder.Id, folder);
            }
            else
            {
                ancestorFolders.Add(folder.Id, folder);
            }
        }

        return new FolderSubtree(
            selectedFolderIdSet,
            downloadFolders,
            ancestorFolders);
    }
}

public class BulkDownloadDetails
{
    public required List<ResolvedBulkDownloadFile> Files { get; init; }
    public required FolderSubtree FolderSubtree { get; init; }

    public void Deconstruct(out List<ResolvedBulkDownloadFile> files, out FolderSubtree folderSubtree)
    {
        files = Files;
        folderSubtree = FolderSubtree;
    }
}

public class ResolvedBulkDownloadFile
{
    public required FileExtId ExternalId {get;init;}
    public required string FullName { get; init; }
    public required string S3KeySecretPart {get; init;}
    public required long SizeInBytes {get; init;}
    public required int? FolderId { get; init; }
    public required FileEncryptionMode EncryptionMode { get; init; }
}

// FolderSubtree holds the full folder context for a bulk download operation.
// It maintains two separate dictionaries:
// - folders: selected folders and their descendants — these determine zip structure
// - ancestorFolders: folders above the selection — only used for building full workspace paths (audit log)
//
// GetAllIds() returns only download folder IDs (used to query files for the zip).
// GetPath() builds relative paths from the selected (top) folder down — used for zip entry paths.
// GetFullPath() builds absolute paths from workspace root — used for audit log entries.
// GetLevelInTree() returns nesting depth relative to the top folder — used for zip ordering.
//
// Folders that belong to ancestorFolders are treated as "outside the selection" by GetPath,
// GetLevelInTree etc. — they return null/0 as if the file were at the zip root.
//todo add unit tests
public class FolderSubtree(
    HashSet<int> topFolderIds,
    Dictionary<int, BulkDownloadFolder> folders,
    Dictionary<int, BulkDownloadFolder> ancestorFolders)
{
    private readonly Dictionary<int, string> _pathsDict = new ();

    // Returns only download folder IDs (selected + descendants), excluding ancestor folders.
    // Used to determine which folders' files should be included in the zip.
    public int[] GetAllIds() => folders.Keys.ToArray();

    // Returns nesting depth relative to the nearest top (selected) folder.
    // Files in ancestor folders or with no folder are treated as root level (0).
    public int GetLevelInTree(int? folderId)
    {
        if (folderId is null)
            return 0;

        // Folder is above the selection — treat as root level for zip purposes
        if (ancestorFolders.ContainsKey(folderId.Value))
            return 0;

        if (topFolderIds.Contains(folderId.Value))
            return 0;

        if (!folders.TryGetValue(folderId.Value, out var folder))
            throw new ArgumentOutOfRangeException(
                paramName: nameof(folderId),
                message: $"FolderId {folderId} was not found in the bulk download folders subtree");

        foreach (var topFolderId in topFolderIds)
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

    // Builds the full path from workspace root to this folder, using both
    // download folders and ancestor folders for name resolution.
    // Used for audit log entries where the complete workspace path is needed.
    public string? GetFullPath(int? folderId)
    {
        if (folderId is null)
            return null;

        var folder = GetAnyFolderOrThrow(folderId.Value);

        var parts = new List<string>();

        foreach (var ancestorId in folder.AncestorFolderIds)
        {
            var ancestor = GetAnyFolderOrThrow(ancestorId);
            parts.Add(ancestor.Name);
        }

        parts.Add(folder.Name);

        return string.Join("/", parts);
    }

    // Looks up a folder in both dictionaries — download folders first, then ancestors.
    private BulkDownloadFolder GetAnyFolderOrThrow(int folderId)
    {
        if (folders.TryGetValue(folderId, out var folder))
            return folder;

        if (ancestorFolders.TryGetValue(folderId, out folder))
            return folder;

        throw new ArgumentOutOfRangeException(
                paramName: nameof(folderId),
                message: $"FolderId {folderId} was not found in the folder subtree or ancestors");
    }

    // Builds a relative path from the nearest top (selected) folder down.
    // Used for zip entry paths. Returns null for files in ancestor folders
    // (they land at the zip root).
    public string? GetPath(int? folderId)
    {
        if (folderId is null)
            return null;

        // Folder is above the selection — file goes to zip root
        if (ancestorFolders.ContainsKey(folderId.Value))
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
                if (topFolderIds.Contains(ancestorId))
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
        if (!folders.TryGetValue(folderId, out var folder))
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