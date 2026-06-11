using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Files.Metadata.Contracts;
using PlikShare.MediaProcessing;
using PlikShare.Folders.Id;
using PlikShare.Folders.List.Contracts;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Folders.List;

public class GetFolderContentQuery(PlikShareDb plikShareDb)
{
    public readonly record struct ExecutionFlags(
		bool GetCurrentFolder,
		bool GetSubfolders,
		FilesExecutionFlag GetFiles,
		bool GetUploads,
		bool ExposeCreatedAt);

	public enum FilesExecutionFlag
	{
		All = 0,
		UploadedByUserOnly
	}

    public const int FirstFilesChunkSize = 200;
    public const int NextFilesChunkSize = 1000;

    public GetFolderContentResponseDto? Execute(
	    WorkspaceContext workspace,
	    FolderExtId folderExternalId,
	    int? boxFolderId,
	    IUserIdentity userIdentity,
	    ExecutionFlags executionFlags,
	    WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        var chunks = ExecuteStreamed(
            workspace: workspace,
            folderExternalId: folderExternalId,
            boxFolderId: boxFolderId,
            userIdentity: userIdentity,
            executionFlags: executionFlags,
            workspaceEncryptionSession: workspaceEncryptionSession);

        if (chunks is null)
            return null;

        CurrentFolderDto? folder = null;
        var subfolders = new List<SubfolderDto>();
        var files = new List<FileDto>();
        var uploads = new List<UploadDto>();

        int? totalFileCount = null;

        foreach (var chunk in chunks)
        {
            folder ??= chunk.Folder;
            totalFileCount ??= chunk.TotalFileCount;

            if (chunk.Subfolders is not null)
                subfolders.AddRange(chunk.Subfolders);

            if (chunk.Files is not null)
                files.AddRange(chunk.Files);

            if (chunk.Uploads is not null)
                uploads.AddRange(chunk.Uploads);
        }

        return new GetFolderContentResponseDto
        {
            Folder = folder,
            Files = files,
            Subfolders = subfolders,
            Uploads = uploads,
            TotalFileCount = totalFileCount
        };
    }

    public IEnumerable<GetFolderContentResponseDto>? ExecuteStreamed(
	    WorkspaceContext workspace,
	    FolderExtId folderExternalId,
	    int? boxFolderId,
	    IUserIdentity userIdentity,
	    ExecutionFlags executionFlags,
	    WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
	    var connection = plikShareDb.OpenConnection();

        try
        {
            //todo what to do with this isEmpty?
            var (isCurrentFolderEmpty, currentFolderId) = TryGetCurrentFolderId(
                workspace,
                folderExternalId,
                boxFolderId,
                connection);

            if (isCurrentFolderEmpty)
            {
                connection.Dispose();
                return null;
            }

            return StreamContent(
                connection,
                workspace,
                currentFolderId,
                boxFolderId,
                userIdentity,
                executionFlags,
                workspaceEncryptionSession);
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static IEnumerable<GetFolderContentResponseDto> StreamContent(
        SqliteConnection connection,
        WorkspaceContext workspace,
        int currentFolderId,
        int? boxFolderId,
        IUserIdentity userIdentity,
        ExecutionFlags executionFlags,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        using (connection)
        {
            var currentFolder = GetCurrentFolder(
                workspace,
                boxFolderId,
                currentFolderId,
                connection,
                executionFlags.GetCurrentFolder,
                workspaceEncryptionSession);

            var subfolders = GetSubfolders(
                currentFolderId,
                userIdentity,
                connection,
                executionFlags.GetSubfolders,
                executionFlags.ExposeCreatedAt,
                workspaceEncryptionSession);

            var uploads = GetUploads(
                workspace,
                userIdentity,
                currentFolderId,
                connection,
                executionFlags.GetUploads,
                workspaceEncryptionSession);

            var totalFileCount = CountFiles(
                workspace,
                userIdentity,
                currentFolderId,
                connection,
                executionFlags.GetFiles);

            var files = EnumerateAllFiles(
                    workspace,
                    userIdentity,
                    currentFolderId,
                    connection,
                    executionFlags.GetFiles == FilesExecutionFlag.All,
                    executionFlags.ExposeCreatedAt,
                    workspaceEncryptionSession)
                .Concat(EnumerateFilesUploadedByUser(
                    workspace,
                    userIdentity,
                    currentFolderId,
                    connection,
                    executionFlags.GetFiles == FilesExecutionFlag.UploadedByUserOnly,
                    executionFlags.ExposeCreatedAt,
                    workspaceEncryptionSession));

            var isFirstChunkYielded = false;
            var batch = new List<FileDto>();

            GetFolderContentResponseDto BuildChunk()
            {
                var chunk = new GetFolderContentResponseDto
                {
                    Folder = isFirstChunkYielded ? null : currentFolder,
                    Subfolders = isFirstChunkYielded ? null : subfolders,
                    Uploads = isFirstChunkYielded ? null : uploads,
                    Files = batch
                };

                isFirstChunkYielded = true;
                batch = [];

                return chunk;
            }

            yield return new GetFolderContentResponseDto
            {
                Folder = null,
                Subfolders = null,
                Files = null,
                Uploads = null,
                TotalFileCount = totalFileCount
            };

            foreach (var file in files)
            {
                batch.Add(file);

                if (batch.Count == (isFirstChunkYielded ? NextFilesChunkSize : FirstFilesChunkSize))
                    yield return BuildChunk();
            }

            if (!isFirstChunkYielded || batch.Count > 0)
                yield return BuildChunk();
        }
    }

    private static List<UploadDto> GetUploads(
	    WorkspaceContext workspace,
	    IUserIdentity userIdentity,
	    int currentFolderId,
	    SqliteConnection connection,
        bool shouldGetUploads,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        if (!shouldGetUploads)
            return [];

	    return connection
		    .Cmd(
			    sql: @"
						SELECT
							fu_external_id,
							fu_file_name,
							fu_file_extension,
							fu_file_content_type,
							fu_file_size_in_bytes,
							(
						        SELECT json_group_array(fup_part_number)
						        FROM fup_file_upload_parts
						        WHERE fup_file_upload_id = fu_id
						        ORDER BY fup_part_number
						    ) AS fu_already_uploaded_part_numbers
						FROM fu_file_uploads
						WHERE
							fu_workspace_id = $workspaceId
		  					AND fu_owner_identity_type = $ownerIdentityType
		  					AND fu_owner_identity = $ownerIdentity
							AND fu_folder_id = $folderId
                            AND fu_is_completed = FALSE
                            AND fu_parent_file_id IS NULL
						ORDER BY 
							fu_id DESC
					",
                readRowFunc: reader => new UploadDto
                {
                    ExternalId = reader.GetString(0),
                    FileName = reader.DecodeEncryptableString(1, workspaceEncryptionSession),
                    FileExtension = reader.DecodeEncryptableString(2, workspaceEncryptionSession),
                    FileContentType = reader.DecodeEncryptableString(3, workspaceEncryptionSession),
                    FileSizeInBytes = reader.GetInt64(4),
                    AlreadyUploadedPartNumbers = reader.GetFromJson<List<int>>(5)
                },
                name: "folder_content.get_uploads")
		    .WithParameter("$workspaceId", workspace.Id)
		    .WithParameter("$ownerIdentityType", userIdentity.IdentityType)
		    .WithParameter("$ownerIdentity", userIdentity.Identity)
		    .WithParameter("$folderId", currentFolderId)
		    .Execute();
    }

    private static int CountFiles(
	    WorkspaceContext workspace,
	    IUserIdentity userIdentity,
	    int currentFolderId,
	    SqliteConnection connection,
        FilesExecutionFlag filesExecutionFlag)
    {
        if (filesExecutionFlag == FilesExecutionFlag.All)
        {
            return connection
                .OneRowCmd(
                    sql: @"
						SELECT COUNT(*)
						FROM fi_files
						WHERE
							fi_workspace_id = $workspaceId
							AND fi_folder_id = $folderId
							AND fi_parent_file_id IS NULL
							AND fi_deleted_at IS NULL
					",
                    readRowFunc: reader => reader.GetInt32(0),
                    name: "folder_content.count_all_files")
                .WithParameter("$workspaceId", workspace.Id)
                .WithParameter("$folderId", currentFolderId)
                .Execute()
                .Value;
        }

        return connection
            .OneRowCmd(
                sql: @"
					SELECT COUNT(*)
					FROM fi_files
					WHERE
						fi_workspace_id = $workspaceId
						AND fi_uploader_identity_type = $uploaderIdentityType
						AND fi_uploader_identity = $uploaderIdentity
						AND fi_folder_id = $folderId
						AND fi_parent_file_id IS NULL
						AND fi_deleted_at IS NULL
				",
                readRowFunc: reader => reader.GetInt32(0),
                name: "folder_content.count_files_uploaded_by_user")
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$uploaderIdentityType", userIdentity.IdentityType)
            .WithParameter("$uploaderIdentity", userIdentity.Identity)
            .WithParameter("$folderId", currentFolderId)
            .Execute()
            .Value;
    }

    private static IEnumerable<FileDto> EnumerateFilesUploadedByUser(
	    WorkspaceContext workspace,
	    IUserIdentity userIdentity,
	    int currentFolderId,
	    SqliteConnection connection,
        bool shouldGetFilesUploadedByUser,
        bool exposeCreatedAt,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        if (!shouldGetFilesUploadedByUser)
            return [];

        var childrenMetadataByParentId = GetChildFilesMetadataByParentId(
            workspace,
            currentFolderId,
            connection);

        long maxPosition = 0;

	    return connection
		    .Cmd(
			    sql: @"
						SELECT
				            fi_id,
				            fi_external_id,
				            fi_name,
				            fi_extension,
				            fi_size_in_bytes,
							TRUE AS fi_was_uploaded_by_user,
                            NOT fi_is_upload_completed,
                            fi_position,
                            CASE WHEN $exposeCreatedAt THEN fi_created_at END AS fi_created_at,
                            fi_metadata AS parent_file_metadata
				        FROM fi_files
				        WHERE
				            fi_workspace_id = $workspaceId
							AND fi_uploader_identity_type = $uploaderIdentityType
							AND fi_uploader_identity =  $uploaderIdentity
				            AND fi_folder_id = $folderId
                            AND fi_parent_file_id IS NULL
                            AND fi_deleted_at IS NULL
						ORDER BY
						    (fi_position IS NULL),
						    fi_position,
						    fi_id DESC
					",
                readRowFunc: reader =>
                {
                    (var position, maxPosition) = ItemPosition.Calculate(
                        storedPosition: reader.GetInt64OrNull(7),
                        maxPosition: maxPosition);

                    var childrenMetadata = childrenMetadataByParentId.GetValueOrDefault(
                        reader.GetInt32(0));

                    return new FileDto
                    {
                        ExternalId = reader.GetString(1),
                        Name = reader.DecodeEncryptableString(2, workspaceEncryptionSession),
                        Extension = reader.DecodeEncryptableString(3, workspaceEncryptionSession),
                        SizeInBytes = reader.GetInt64(4),
                        WasUploadedByUser = reader.GetBoolean(5),
                        IsLocked = reader.GetBoolean(6),
                        CreatedAt = reader.GetDateTimeOffsetOrNull(8)?.UtcDateTime,
                        Position = position,
                        Metadata = FileMetadataFactory.Prepare(
                            thumbnail: ThumbnailEtagsMetadata.PrepareDto(childrenMetadata, workspaceEncryptionSession),
                            dimensions: ImageDimensionsMetadata.Read(reader, 9, workspaceEncryptionSession) is { } dimensions
                                ? new DimensionsMetadataDto { Width = dimensions.Width, Height = dimensions.Height }
                                : null)
                    };
                },
                name: "folder_content.get_files_uploaded_by_user")
		    .WithParameter("$uploaderIdentityType", userIdentity.IdentityType)
		    .WithParameter("$uploaderIdentity", userIdentity.Identity)
		    .WithParameter("$workspaceId", workspace.Id)
		    .WithParameter("$folderId", currentFolderId)
		    .WithParameter("$exposeCreatedAt", exposeCreatedAt)
		    .ExecuteEnumerable();
    }

    private static IEnumerable<FileDto> EnumerateAllFiles(
	    WorkspaceContext workspace,
	    IUserIdentity userIdentity,
	    int currentFolderId,
	    SqliteConnection connection,
        bool shouldGetAllFiles,
        bool exposeCreatedAt,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        if (!shouldGetAllFiles)
            return [];

        var childrenMetadataByParentId = GetChildFilesMetadataByParentId(
            workspace,
            currentFolderId,
            connection);

        long maxPosition = 0;

	    return connection
		    .Cmd(
			    sql: @"
					 SELECT
				        fi_id,
				        fi_external_id,
				        fi_name,
				        fi_extension,
				        fi_size_in_bytes,
						(
							fi_uploader_identity_type = $uploaderIdentityType
							AND fi_uploader_identity =  $uploaderIdentity
						) AS fi_was_uploaded_by_user,
                        NOT fi_is_upload_completed,
                        fi_position,
                        CASE WHEN $exposeCreatedAt THEN fi_created_at END AS fi_created_at,
                        fi_metadata AS parent_file_metadata
				    FROM fi_files
				    WHERE
				        fi_workspace_id = $workspaceId
				        AND fi_folder_id = $folderId
                        AND fi_parent_file_id IS NULL
                        AND fi_deleted_at IS NULL
					 ORDER BY
						(fi_position IS NULL),
						fi_position,
						fi_id DESC
				",
                readRowFunc: reader =>
                {
                    (var position, maxPosition) = ItemPosition.Calculate(
                        storedPosition: reader.GetInt64OrNull(7),
                        maxPosition: maxPosition);

                    var childrenMetadata = childrenMetadataByParentId.GetValueOrDefault(
                        reader.GetInt32(0));

                    return new FileDto
                    {
                        ExternalId = reader.GetString(1),
                        Name = reader.DecodeEncryptableString(2, workspaceEncryptionSession),
                        Extension = reader.DecodeEncryptableString(3, workspaceEncryptionSession),
                        SizeInBytes = reader.GetInt64(4),
                        WasUploadedByUser = reader.GetBoolean(5),
                        IsLocked = reader.GetBoolean(6),
                        CreatedAt = reader.GetDateTimeOffsetOrNull(8)?.UtcDateTime,
                        Position = position,
                        Metadata = FileMetadataFactory.Prepare(
                            thumbnail: ThumbnailEtagsMetadata.PrepareDto(childrenMetadata, workspaceEncryptionSession),
                            dimensions: ImageDimensionsMetadata.Read(reader, 9, workspaceEncryptionSession) is { } dimensions
                                ? new DimensionsMetadataDto { Width = dimensions.Width, Height = dimensions.Height }
                                : null)
                    };
                },
                name: "folder_content.get_all_files")
		    .WithParameter("$uploaderIdentityType", userIdentity.IdentityType)
		    .WithParameter("$uploaderIdentity", userIdentity.Identity)
		    .WithParameter("$workspaceId", workspace.Id)
		    .WithParameter("$folderId", currentFolderId)
		    .WithParameter("$exposeCreatedAt", exposeCreatedAt)
		    .ExecuteEnumerable();
    }

    private static Dictionary<int, List<string>> GetChildFilesMetadataByParentId(
	    WorkspaceContext workspace,
	    int currentFolderId,
	    SqliteConnection connection)
    {
        return connection
            .AggregateRows(
                sql: @"
                    SELECT
                        fi_parent_file_id,
                        CAST(fi_metadata AS TEXT)
                    FROM fi_files
                    WHERE
                        fi_workspace_id = $workspaceId
                        AND fi_folder_id = $folderId
                        AND fi_parent_file_id IS NOT NULL
                        AND fi_deleted_at IS NULL
                        AND fi_is_upload_completed = TRUE
                        AND fi_metadata IS NOT NULL
                ",
                seed: new Dictionary<int, List<string>>(),
                aggregateRowFunc: (childrenMetadataByParentId, reader) =>
                {
                    var parentFileId = reader.GetInt32(0);

                    if (!childrenMetadataByParentId.TryGetValue(parentFileId, out var metadataList))
                    {
                        metadataList = [];
                        childrenMetadataByParentId[parentFileId] = metadataList;
                    }

                    metadataList.Add(reader.GetString(1));

                    return childrenMetadataByParentId;
                },
                name: "folder_content.get_files_children")
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$folderId", currentFolderId)
            .Execute();
    }

    private static CurrentFolderDto? GetCurrentFolder(
	    WorkspaceContext workspace,
	    int? boxFolderId,
	    int currentFolderId,
	    SqliteConnection connection,
        bool shouldGetCurrentFolder,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        if (!shouldGetCurrentFolder)
            return null;

	    var result = connection
		    .OneRowCmd(
			    sql: """
			     SELECT
			        cf.fo_external_id,
			        cf.fo_name,
			        (
			            SELECT json_group_array(json_object(
			                'name', sub.fo_name,
			                'externalId', sub.fo_external_id
			            ))
			            FROM (
			                SELECT af.fo_name, af.fo_external_id
			                FROM fo_folders AS af
			                WHERE
			                    af.fo_id IN (
			                        SELECT value FROM json_each(cf.fo_ancestor_folder_ids)
			                    )
			                    AND af.fo_workspace_id = $workspaceId
			                    AND af.fo_is_being_deleted = FALSE
			                    AND (
			                        $boxFolderId IS NULL
			                        OR $boxFolderId IN (
			                            SELECT value FROM json_each(af.fo_ancestor_folder_ids)
			                        )
			                    )
			                ORDER BY json_array_length(af.fo_ancestor_folder_ids)
			            ) AS sub
			        ) AS fo_ancestors
			    FROM fo_folders AS cf
			    WHERE
			        cf.fo_id = $folderId
			        AND (
			            $boxFolderId IS NULL
			            OR $boxFolderId IN (
			                SELECT value FROM json_each(cf.fo_ancestor_folder_ids)
			            )
			        )
			    """,
				readRowFunc: reader => new CurrentFolderDto
                {
                    ExternalId = reader.GetString(0),
					Name = reader.DecodeEncryptableString(1, workspaceEncryptionSession),
					Ancestors = reader.GetFromJson<List<AncestorFolderDto>>(2, workspaceEncryptionSession)
                },
                name: "folder_content.get_current_folder")
		    .WithParameter("$workspaceId", workspace.Id)
		    .WithParameter("$folderId", currentFolderId)
		    .WithParameter("$boxFolderId", boxFolderId)
            .Execute();

        return result.IsEmpty
            ? null
            : result.Value;
    }

    private static List<SubfolderDto> GetSubfolders(
	    int currentFolderId,
	    IUserIdentity userIdentity,
	    SqliteConnection connection,
        bool shouldGetSubfolders,
        bool exposeCreatedAt,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        if (!shouldGetSubfolders)
            return [];

        long maxPosition = 0;

	    return connection
		    .Cmd(
			    sql: @"
			        SELECT
			            fo_external_id,
			            fo_name,
						CASE
	                        WHEN fo_creator_identity_type = $creatorIdentityType AND fo_creator_identity = $creatorIdentity THEN TRUE
							ELSE FALSE
	                    END AS fo_was_created_by_user,
			            CASE
			                WHEN $exposeCreatedAt OR (fo_creator_identity_type = $creatorIdentityType AND fo_creator_identity = $creatorIdentity) THEN fo_created_at
			            END AS fo_created_at,
			            fo_position
			        FROM fo_folders
			        WHERE
			            fo_parent_folder_id = $parentFolderId
			            AND fo_is_being_deleted = FALSE
			        ORDER BY
			            (fo_position IS NULL),
			            fo_position,
			            fo_id
				",
                readRowFunc: reader =>
                {
                    (var position, maxPosition) = ItemPosition.Calculate(
                        storedPosition: reader.GetInt64OrNull(4),
                        maxPosition: maxPosition);

                    return new SubfolderDto
                    {
                        ExternalId = reader.GetString(0),
                        Name = reader.DecodeEncryptableString(1, workspaceEncryptionSession),
                        WasCreatedByUser = reader.GetBoolean(2),
                        CreatedAt = reader.GetDateTimeOffsetOrNull(3)?.UtcDateTime,
                        Position = position
                    };
                },
                name: "folder_content.get_subfolders")
		    .WithParameter("$parentFolderId", currentFolderId)
		    .WithParameter("$creatorIdentityType", userIdentity.IdentityType)
		    .WithParameter("$creatorIdentity", userIdentity.Identity)
		    .WithParameter("$exposeCreatedAt", exposeCreatedAt)
		    .Execute();
    }

    private static SQLiteOneRowCommandResult<int> TryGetCurrentFolderId(
	    WorkspaceContext workspace, 
	    FolderExtId folderExternalId, 
	    int? boxFolderId, 
	    SqliteConnection connection)
    {
	    return connection
		    .OneRowCmd(
			    sql: @"
					SELECT fo_id
					FROM fo_folders
					WHERE 
					    fo_external_id = $folderExternalId
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
			    readRowFunc: reader => reader.GetInt32(0),
			    name: "folder_content.try_get_current_folder_id")
		    .WithParameter("$workspaceId", workspace.Id)
		    .WithParameter("$folderExternalId", folderExternalId.Value)
		    .WithParameter("$boxFolderId", boxFolderId)
		    .Execute();
    }
}