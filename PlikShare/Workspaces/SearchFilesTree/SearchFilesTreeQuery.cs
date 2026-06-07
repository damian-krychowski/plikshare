using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Files.Metadata.Contracts;
using PlikShare.Folders.Id;
using PlikShare.Folders.List;
using PlikShare.MediaProcessing;
using PlikShare.Storages.Encryption;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.SearchFilesTree.Contracts;

namespace PlikShare.Workspaces.SearchFilesTree;

public class SearchFilesTreeQuery(PlikShareDb plikShareDb)
{
    // Safety cap on result-set size. Bumped from 1000 after the tree-view
    // switched to window-driven virtualization — DOM render cost is no longer
    // proportional to match count, so the limit is now only about backend
    // memory (matchingFiles list + SQL result) and protobuf payload size.
    // Keeps a safety net against pathological queries (e.g. single-letter
    // phrase matching everything in a million-file workspace).
    public const int TooManyResultsThreshold = 10000;

    public SearchFilesTreeResponseDto Execute(
        WorkspaceContext workspace,
        SearchFilesTreeRequestDto request,
        IUserIdentity userIdentity,
        int? boxFolderId,
        bool exposeCreatedAt,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        // Full-encryption workspaces store fo_name / fi_name / fi_extension as pse: envelopes, so a
        // plaintext SQL LIKE never matches. We decrypt inline via the app_decrypt_metadata UDF on a
        // NON-POOLED connection (the UDF closure captures the session's live DEKs — it must die with
        // this connection and never be observable by another request that draws the same pooled one).
        // None/Managed stay on the cheap pooled connection + plaintext columns.
        var isEncrypted = workspace.EncryptionType == StorageEncryptionType.Full;

        using var connection = isEncrypted
            ? plikShareDb.OpenNonPooledConnection()
            : plikShareDb.OpenConnection();

        using var udfScope = isEncrypted
            ? connection.RegisterDecryptMetadataFunction(
                new Dictionary<int, WorkspaceEncryptionSession>
                {
                    [workspace.Id] = workspaceEncryptionSession!
                })
            : null;

        var doesParentFolderExist = TryGetParentFolderId(
            connection,
            workspace.Id,
            request.FolderExternalId,
            userIdentity,
            boxFolderId,
            out var parentFolderId);

        if (!doesParentFolderExist)
        {
            return new SearchFilesTreeResponseDto
            {
                Files = [],
                Folders = [],
                FolderExternalIds = [],
                TooManyResultsCounter = -1
            };
        }

        var matchingFiles = GetMatchingFiles(
            workspaceId: workspace.Id,
            parentFolderId: parentFolderId,
            phrase: request.Phrase,
            userIdentity,
            exposeCreatedAt,
            isEncrypted: isEncrypted,
            workspaceEncryptionSession: workspaceEncryptionSession,
            connection);

        if (matchingFiles.Count > TooManyResultsThreshold)
        {
            return new SearchFilesTreeResponseDto
            {
                Files = [],
                Folders = [],
                FolderExternalIds = [],
                TooManyResultsCounter = matchingFiles.Count
            };
        }

        var folderIds = matchingFiles
            .Where(fi => fi.FolderId.HasValue)
            .Select(fi => fi.FolderId!.Value)
            .Distinct()
            .ToList();

        var allFolders = GetFoldersAndAncestors(
            folderIds: folderIds,
            parentFolderId: parentFolderId,
            userIdentity: userIdentity,
            exposeCreatedAt: exposeCreatedAt,
            isEncrypted: isEncrypted,
            connection: connection);

        var response = BuildResponse(
            allFolders: allFolders,
            matchingFiles: matchingFiles);

        return response;
    }

    private static SearchFilesTreeResponseDto BuildResponse(
        List<Folder> allFolders, 
        List<File> matchingFiles)
    {
        var response = new SearchFilesTreeResponseDto
        {
            FolderExternalIds = [],
            Files = [],
            Folders = [],
            TooManyResultsCounter = -1
        };

        var folderIdFolderIndexMap = new Dictionary<int, int>();

        for (var i = 0; i < allFolders.Count; i++)
        {
            var folder = allFolders[i];
            folderIdFolderIndexMap.Add(folder.Id, i);
            response.FolderExternalIds.Add(folder.ExternalId);
        }

        for (var i = 0; i < allFolders.Count; i++)
        {
            var folder = allFolders[i];
            var idIndex = folderIdFolderIndexMap[folder.Id];
            var parentIdIndex = GetParentIdIndex(
                parentId: folder.ParentId,
                folderIdFolderIndexMap: folderIdFolderIndexMap);

            response.Folders.Add(new SearchFilesTreeFolderItemDto
            {
                IdIndex = idIndex,
                ParentIdIndex = parentIdIndex ?? -1,
                Name = folder.Name,
                WasCreatedByUser = folder.WasCreatedByUser,
                CreatedAt = folder.CreatedAt?.DateTime,
                Position = folder.Position
            });
        }

        for (var i = 0; i < matchingFiles.Count; i++)
        {
            var file = matchingFiles[i];
            var folderIdIndex = GetParentIdIndex(
                parentId: file.FolderId,
                folderIdFolderIndexMap: folderIdFolderIndexMap);

            response.Files.Add(new SearchFilesTreeFileItemDto
            {
                ExternalId = file.ExternalId,
                Name = file.Name,
                Extension = file.Extension,
                IsLocked = file.IsLocked,
                SizeInBytes = file.SizeInBytes,
                WasUploadedByUser = file.WasUploadedByUser,
                FolderIdIndex = folderIdIndex ?? -1,
                CreatedAt = file.CreatedAt?.DateTime,
                Position = file.Position,
                Metadata = FileMetadataFactory.Prepare(
                    thumbnail: file.MiniThumbnailEtag is { } etag
                        ? new ThumbnailMetadataDto { MiniEtag = etag }
                        : null,
                    dimensions: file.Width is { } width && file.Height is { } height
                        ? new DimensionsMetadataDto { Width = width, Height = height }
                        : null)
            });
        }

        return response;
    }

    private static int? GetParentIdIndex(
        int? parentId, 
        Dictionary<int, int> folderIdFolderIndexMap)
    {
        if (parentId is null)
            return null;

        if (folderIdFolderIndexMap.TryGetValue(parentId.Value, out var index))
            return index;

        return null;
    }

    private bool TryGetParentFolderId(
        SqliteConnection connection, 
        int workspaceId,
        FolderExtId? externalId,
        IUserIdentity userIdentity,
        int? boxFolderId,
        out int? parentFolder)
    {
        if (externalId is null)
        {
            //if box is not provided then we have top folder scenario (parentFolder = null)
            //otherwise we have box top folder scenario (parentFolder = boxFolderId.Value)
            parentFolder = boxFolderId;
            return true;
        }

        var folder = connection
            .OneRowCmd(
                sql: @"
                    SELECT fo_id	            	
			        FROM fo_folders
			        WHERE
                        fo_workspace_id = $workspaceId
			            AND fo_external_id = $parentFolderExternalId
			            AND fo_is_being_deleted = FALSE
                        AND (
                            $boxFolderId IS NULL
                            OR $boxFolderId IN (
                                SELECT value FROM json_each(fo_ancestor_folder_ids)
                            )
                        )
                ",
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$workspaceId", workspaceId)
            .WithParameter("$parentFolderExternalId", externalId.Value.Value)
            .WithParameter("$boxFolderId", boxFolderId)
            .WithParameter("$creatorIdentityType", userIdentity.IdentityType)
            .WithParameter("$creatorIdentity", userIdentity.Identity)
            .Execute();

        if (folder.IsEmpty)
        {
            parentFolder = null;
            return false;
        }

        parentFolder = folder.Value;
        return true;
    }

    private List<File> GetMatchingFiles(
        int workspaceId,
        int? parentFolderId,
        string phrase,
        IUserIdentity userIdentity,
        bool exposeCreatedAt,
        bool isEncrypted,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        SqliteConnection connection)
    {
        // In Full-encryption the name/extension columns are pse: envelopes — wrap them in the
        // app_decrypt_metadata UDF so both the LIKE filter and the projected values are plaintext.
        var fiName = isEncrypted ? "app_decrypt_metadata(fi_name, fi_workspace_id)" : "fi_name";
        var fiExtension = isEncrypted ? "app_decrypt_metadata(fi_extension, fi_workspace_id)" : "fi_extension";

        return connection
            .Cmd(
                sql: $@"
                    SELECT
				        fi_external_id,
                        fi_folder_id,
				        {fiName} AS fi_name_plain,
				        {fiExtension} AS fi_extension_plain,
				        fi_size_in_bytes,
						(
							fi_uploader_identity_type = $uploaderIdentityType
							AND fi_uploader_identity =  $uploaderIdentity
						) AS fi_was_uploaded_by_user,
                        NOT fi_is_upload_completed,
                        CASE WHEN $exposeCreatedAt THEN fi_created_at END AS fi_created_at,
                        COALESCE(fi_position, 0) AS fi_position,
                        (
                            SELECT json_group_array(CAST(child_fi.fi_metadata AS TEXT))
                            FROM fi_files AS child_fi
                            WHERE child_fi.fi_parent_file_id = fi_files.fi_id
                                AND child_fi.fi_workspace_id = $workspaceId
                                AND child_fi.fi_deleted_at IS NULL
                                AND child_fi.fi_is_upload_completed = TRUE
                                AND child_fi.fi_metadata IS NOT NULL
                        ) AS child_thumbnail_metadata,
                        fi_metadata AS parent_file_metadata
				    FROM fi_files
                    LEFT JOIN fo_folders
                        ON fo_id = fi_folder_id
				    WHERE
				        fi_workspace_id = $workspaceId
                        AND fi_parent_file_id IS NULL
                        AND fi_deleted_at IS NULL
                        AND ({fiName} || {fiExtension}) LIKE $query
                        AND (
                            fi_folder_id IS NULL
                            OR fo_is_being_deleted = FALSE
                        )
                        AND (
                            $parentFolderId IS NULL
                            OR (
                                fi_folder_id IS NOT NULL
                                AND (
                                    $parentFolderId = fo_id
                                    OR $parentFolderId IN (
                                        SELECT value FROM json_each(fo_ancestor_folder_ids)
                                    )
                                )
                            )
                        )
					 ORDER BY
						fi_id DESC
                ",
                readRowFunc: reader => new File
                {
                    ExternalId = reader.GetString(0),
                    FolderId = reader.GetInt32OrNull(1),
                    Name = reader.GetString(2),
                    Extension = reader.GetString(3),
                    SizeInBytes = reader.GetInt64(4),
                    WasUploadedByUser = reader.GetBoolean(5),
                    IsLocked = reader.GetBoolean(6),
                    CreatedAt = reader.GetDateTimeOffsetOrNull(7),
                    Position = reader.GetInt64(8),
                    
                    // child_thumbnail_metadata is the raw (still-encrypted in Full) fi_metadata json
                    // array; GetMiniEtag decodes it in managed code — session is null for None/Managed
                    // (plaintext passthrough) and the real session for Full.
                    MiniThumbnailEtag = MiniThumbnailMetadata.GetMiniEtag(reader, 9, workspaceEncryptionSession),
                    Width = ImageDimensionsMetadata.Read(reader, 10, workspaceEncryptionSession)?.Width,
                    Height = ImageDimensionsMetadata.Read(reader, 10, workspaceEncryptionSession)?.Height
                })
            .WithParameter("$workspaceId", workspaceId)
            .WithParameter("$uploaderIdentityType", userIdentity.IdentityType)
            .WithParameter("$uploaderIdentity", userIdentity.Identity)
            .WithParameter("$query", $"%{phrase}%")
            .WithParameter("$parentFolderId", parentFolderId)
            .WithParameter("$exposeCreatedAt", exposeCreatedAt)
            .Execute();
    }

    private List<Folder> GetFoldersAndAncestors(
        List<int> folderIds,
        int? parentFolderId,
        IUserIdentity userIdentity,
        bool exposeCreatedAt,
        bool isEncrypted,
        SqliteConnection connection)
    {
        // Folder names are pse: envelopes in Full-encryption — decrypt inline so the result-tree
        // shows plaintext folder paths instead of ciphertext.
        var foName = isEncrypted ? "app_decrypt_metadata(fo_name, fo_workspace_id)" : "fo_name";

        return connection
            .Cmd(
                sql: $@"
                    WITH all_folder_ids AS (
                        SELECT value AS fo_id
                        FROM json_each($folderIds)

                        UNION

                        SELECT
                            ancestor.value AS fo_id
                        FROM fo_folders, json_each(fo_ancestor_folder_ids) AS ancestor
                        WHERE fo_id IN (
                            SELECT value FROM json_each($folderIds)
                        )
                    )
                    SELECT
                        fo_id,
                        fo_external_id,
                        fo_parent_folder_id,
			            {foName} AS fo_name_plain,
						CASE
	                        WHEN fo_creator_identity_type = $creatorIdentityType AND fo_creator_identity = $creatorIdentity THEN TRUE
							ELSE FALSE
	                    END AS fo_was_created_by_user,
			            CASE
	                        WHEN $exposeCreatedAt OR (fo_creator_identity_type = $creatorIdentityType AND fo_creator_identity = $creatorIdentity) THEN fo_created_at
	                    END AS fo_created_at,
                        COALESCE(fo_position, 0) AS fo_position
                    FROM fo_folders
                    WHERE
                        fo_id IN (
                            SELECT fo_id FROM all_folder_ids
                        )
                        AND (
                            $parentFolderId IS NULL
                            OR $parentFolderId IN (
                                SELECT value FROM json_each(fo_ancestor_folder_ids)
                            )
                        )
                    ORDER BY fo_id
                ",
                readRowFunc: reader => new Folder
                {
                    Id = reader.GetInt32(0),
                    ExternalId = reader.GetString(1),
                    ParentId = reader.GetInt32OrNull(2),
                    Name = reader.GetString(3),
                    WasCreatedByUser = reader.GetBoolean(4),
                    CreatedAt = reader.GetDateTimeOffsetOrNull(5),
                    Position = reader.GetInt64(6)
                })
            .WithJsonParameter("$folderIds", folderIds)
            .WithParameter("$parentFolderId", parentFolderId)
            .WithParameter("$creatorIdentityType", userIdentity.IdentityType)
            .WithParameter("$creatorIdentity", userIdentity.Identity)
            .WithParameter("$exposeCreatedAt", exposeCreatedAt)
            .Execute();
    }

    private class Folder
    {
        public required int Id { get; init; }
        public required string ExternalId { get; init; }
        public required int? ParentId { get; init; }
        public required string Name { get; init; }
        public required bool WasCreatedByUser { get; init; }
        public required DateTimeOffset? CreatedAt { get; init; }
        public required long Position { get; init; }
    }

    public class File
    {
        public required string ExternalId { get; init; }
        public required string Name { get; init; }
        public required string Extension { get; init; }
        public required long SizeInBytes { get; init; }
        public required bool IsLocked { get; init; }
        public required bool WasUploadedByUser { get; init; }
        public required int? FolderId { get; init; }
        public required DateTimeOffset? CreatedAt { get; init; }
        public required long Position { get; init; }
        public required string? MiniThumbnailEtag { get; init; }
        public required int? Width { get; init; }
        public required int? Height { get; init; }
    }
}