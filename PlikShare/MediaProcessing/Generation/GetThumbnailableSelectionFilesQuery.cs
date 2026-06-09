using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Workspaces.Cache;

namespace PlikShare.MediaProcessing.Generation;

// Resolves an include/exclude tree selection (selected/excluded folder + file external ids) into the
// thumbnailable source files to process: directly selected files plus every file recursively under a
// selected folder, minus excluded subtrees and minus excluded files. Derived files (thumbnails,
// attachments) are skipped via fi_parent_file_id IS NULL — they inherit the parent's fi_folder_id, so
// folder expansion would otherwise pull existing thumbnails back in as sources. A single SELECT
// returns the rows, decodes the extension and drops non-image/video files in memory — no second
// round-trip to re-read the same rows by id.
public class GetThumbnailableSelectionFilesQuery(PlikShareDb plikShareDb)
{
    public CountResult ExecuteCount(
        WorkspaceContext workspace,
        List<string> selectedFolders,
        List<string> selectedFiles,
        List<string> excludedFolders,
        List<string> excludedFiles,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        if (selectedFiles.Count == 0 && selectedFolders.Count == 0)
            return new CountResult(0, 0);

        using var connection = plikShareDb.OpenConnection();

        var folderIds = selectedFolders.Count > 0
            ? ResolveFolderIds(
                workspace: workspace,
                selectedFolders: selectedFolders,
                excludedFolders: excludedFolders,
                connection: connection)
            : [];

        if (selectedFiles.Count == 0 && folderIds.Count == 0)
            return new CountResult(0, 0);

        return CountThumbnailableFiles(
            workspace: workspace,
            selectedFiles: selectedFiles,
            folderIds: folderIds,
            excludedFiles: excludedFiles,
            workspaceEncryptionSession: workspaceEncryptionSession,
            connection: connection);
    }

    private static CountResult CountThumbnailableFiles(
       WorkspaceContext workspace,
       List<string> selectedFiles,
       List<int> folderIds,
       List<string> excludedFiles,
       WorkspaceEncryptionSession? workspaceEncryptionSession,
       SqliteConnection connection)
    {
        return connection
            .AggregateRows(
                sql: """
                     SELECT
                         fi_extension,
                         fi_size_in_bytes
                     FROM fi_files
                     WHERE
                         fi_workspace_id = $workspaceId
                         AND fi_deleted_at IS NULL
                         AND fi_is_upload_completed = TRUE
                         AND fi_parent_file_id IS NULL
                         AND (
                             fi_external_id IN (SELECT value FROM json_each($selectedFileExternalIds))
                             OR fi_folder_id IN (SELECT value FROM json_each($folderIds))
                         )
                         AND fi_external_id NOT IN (SELECT value FROM json_each($excludedFileExternalIds))
                     """,
                seed: new CountResult(0, 0),
                aggregateRowFunc: (acc, reader) =>
                {
                    var extension = reader.DecodeEncryptableString(
                        0,
                        workspaceEncryptionSession);

                    if (!ContentTypeHelper.IsThumbnailable(extension))
                        return acc;

                    return new CountResult(
                        FilesCount: acc.FilesCount + 1,
                        TotalSizeInBytes: acc.TotalSizeInBytes + reader.GetInt64(1));
                },
                name: "media.thumbnailable_files.count")
            .WithParameter("$workspaceId", workspace.Id)
            .WithJsonParameter("$selectedFileExternalIds", selectedFiles)
            .WithJsonParameter("$folderIds", folderIds)
            .WithJsonParameter("$excludedFileExternalIds", excludedFiles)
            .Execute();
    }

    public List<ThumbnailableFile> Execute(
        WorkspaceContext workspace,
        List<string> selectedFolders,
        List<string> selectedFiles,
        List<string> excludedFolders,
        List<string> excludedFiles,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        if (selectedFiles.Count == 0 && selectedFolders.Count == 0)
            return [];

        using var connection = plikShareDb.OpenConnection();

        var folderIds = selectedFolders.Count > 0
            ? ResolveFolderIds(
                workspace: workspace,
                selectedFolders: selectedFolders,
                excludedFolders: excludedFolders,
                connection: connection)
            : [];

        if (selectedFiles.Count == 0 && folderIds.Count == 0)
            return [];

        return FetchThumbnailableFiles(
            workspace: workspace,
            selectedFiles: selectedFiles,
            folderIds: folderIds,
            excludedFiles: excludedFiles,
            workspaceEncryptionSession: workspaceEncryptionSession,
            connection: connection);
    }

    private static List<ThumbnailableFile> FetchThumbnailableFiles(
        WorkspaceContext workspace,
        List<string> selectedFiles,
        List<int> folderIds,
        List<string> excludedFiles,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        SqliteConnection connection)
    {
        return connection
            .AggregateRows(
                sql: """
                     SELECT
                         fi_id,
                         fi_size_in_bytes,
                         fi_extension,
                         fi_encryption_key_version,
                         fi_encryption_salt,
                         fi_encryption_nonce_prefix,
                         fi_encryption_chain_salts,
                         fi_encryption_format_version
                     FROM fi_files
                     WHERE
                         fi_workspace_id = $workspaceId
                         AND fi_deleted_at IS NULL
                         AND fi_is_upload_completed = TRUE
                         AND fi_parent_file_id IS NULL
                         AND (
                             fi_external_id IN (SELECT value FROM json_each($selectedFileExternalIds))
                             OR fi_folder_id IN (SELECT value FROM json_each($folderIds))
                         )
                         AND fi_external_id NOT IN (SELECT value FROM json_each($excludedFileExternalIds))
                     """,
                seed: new List<ThumbnailableFile>(),
                aggregateRowFunc: (acc, reader) =>
                {
                    var extension = reader.DecodeEncryptableString(
                        2,
                        workspaceEncryptionSession);

                    if (!ContentTypeHelper.IsThumbnailable(extension))
                        return acc;

                    acc.Add(new ThumbnailableFile
                    {
                        FileId = reader.GetInt32(0),
                        SizeInBytes = reader.GetInt64(1),
                        Extension = extension,

                        EncryptionMetadata = reader.GetByteOrNull(3) is { } keyVersion
                            ? new FileEncryptionMetadata
                            {
                                KeyVersion = keyVersion,
                                Salt = reader.GetFieldValue<byte[]>(4),
                                NoncePrefix = reader.GetFieldValue<byte[]>(5),
                                ChainStepSalts = KeyDerivationChain.Deserialize(
                                    reader.GetFieldValueOrNull<byte[]>(6)),
                                FormatVersion = reader.GetByteOrNull(7) ?? 1
                            }
                            : null
                    });

                    return acc;
                },
                name: "media.thumbnailable_files.fetch")
            .WithParameter("$workspaceId", workspace.Id)
            .WithJsonParameter("$selectedFileExternalIds", selectedFiles)
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
                },
                name: "media.thumbnailable_files.resolve_request_folders")
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
                readRowFunc: reader => reader.GetInt32(0),
                name: "media.thumbnailable_files.resolve_descendants")
            .WithParameter("$workspaceId", workspace.Id)
            .WithJsonParameter("$selectedFolderIds", selectedFolderIds)
            .WithJsonParameter("$excludedFolderIds", excludedFolderIds)
            .Execute();

        // The descendant query only returns folders that have a selected folder among their
        // ancestors — the top selected folders themselves must be added back.
        descendantFolderIds.AddRange(selectedFolderIds);

        return descendantFolderIds;
    }

    public sealed record ThumbnailableFile
    {
        public required int FileId { get; init; }
        public required long SizeInBytes { get; init; }
        public required string Extension { get; init; }
        public required FileEncryptionMetadata? EncryptionMetadata { get; init; }

        public bool IsVideo()
        {
            return ContentTypeHelper.GetFileTypeFromExtension(Extension) == FileType.Video;
        }
    }

    public readonly record struct CountResult(
        int FilesCount,
        long TotalSizeInBytes);
}
