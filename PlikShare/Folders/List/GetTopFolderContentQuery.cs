using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Files.Metadata.Contracts;
using PlikShare.MediaProcessing;
using PlikShare.Folders.List.Contracts;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Folders.List;

public class GetTopFolderContentQuery(PlikShareDb plikShareDb)
{
    public IEnumerable<GetTopFolderContentResponseDto> ExecuteStreamed(
	    WorkspaceContext workspace,
	    IUserIdentity userIdentity,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
	    using var connection = plikShareDb.OpenConnection();

	    var folders = GetFolders(
		    workspace,
			userIdentity,
			workspaceEncryptionSession,
		    connection);

	    var uploads = GetUploads(
		    workspace,
		    userIdentity,
		    connection,
		    workspaceEncryptionSession);

        var totalFileCount = CountFiles(
            workspace,
            connection);

	    var files = EnumerateFiles(
		    workspace,
			userIdentity,
		    connection,
		    workspaceEncryptionSession);

        var isFirstChunkYielded = false;
        var batch = new List<FileDto>();

        GetTopFolderContentResponseDto BuildChunk()
        {
            var chunk = new GetTopFolderContentResponseDto
            {
                Subfolders = isFirstChunkYielded ? null : folders,
                Uploads = isFirstChunkYielded ? null : uploads,
                Files = batch
            };

            isFirstChunkYielded = true;
            batch = [];

            return chunk;
        }

        yield return new GetTopFolderContentResponseDto
        {
            Subfolders = null,
            Uploads = null,
            Files = [],
            TotalFileCount = totalFileCount
        };

        foreach (var file in files)
        {
            batch.Add(file);

            if (batch.Count == (isFirstChunkYielded ? GetFolderContentQuery.NextFilesChunkSize : GetFolderContentQuery.FirstFilesChunkSize))
                yield return BuildChunk();
        }

        if (!isFirstChunkYielded || batch.Count > 0)
            yield return BuildChunk();
    }

    private static List<UploadDto> GetUploads(
	    WorkspaceContext workspace,
	    IUserIdentity userIdentity,
	    SqliteConnection connection,
	    WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
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
						AND fu_folder_id IS NULL
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
                name: "top_folder_content.get_uploads")
		    .WithParameter("$workspaceId", workspace.Id)
		    .WithParameter("$ownerIdentityType", userIdentity.IdentityType)
		    .WithParameter("$ownerIdentity", userIdentity.Identity)
		    .Execute();
    }

    private static int CountFiles(
	    WorkspaceContext workspace,
	    SqliteConnection connection)
    {
        return connection
            .OneRowCmd(
                sql: @"
					SELECT COUNT(*)
					FROM fi_files
					WHERE
						fi_workspace_id = $workspaceId
						AND fi_folder_id IS NULL
						AND fi_parent_file_id IS NULL
						AND fi_deleted_at IS NULL
				",
                readRowFunc: reader => reader.GetInt32(0),
                name: "top_folder_content.count_files")
            .WithParameter("$workspaceId", workspace.Id)
            .Execute()
            .Value;
    }

    private static IEnumerable<FileDto> EnumerateFiles(
	    WorkspaceContext workspace,
        IUserIdentity userIdentity,
        SqliteConnection connection,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        var childrenMetadataByParentId = GetChildFilesMetadataByParentId(
            workspace,
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
                        fi_created_at,
                        fi_metadata AS parent_file_metadata
		            FROM fi_files
		            WHERE
		                fi_workspace_id = $workspaceId
		                AND fi_folder_id IS NULL
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
                name: "top_folder_content.get_files")
		    .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$uploaderIdentityType", userIdentity.IdentityType)
            .WithParameter("$uploaderIdentity", userIdentity.Identity)
            .ExecuteEnumerable();
    }

    private static Dictionary<int, List<string>> GetChildFilesMetadataByParentId(
	    WorkspaceContext workspace,
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
                        AND fi_folder_id IS NULL
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
                name: "top_folder_content.get_files_children")
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();
    }

    private static List<SubfolderDto> GetFolders(
	    WorkspaceContext workspace,
        IUserIdentity userIdentity,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        SqliteConnection connection)
    {
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
			            fo_created_at,
			            fo_position
		            FROM
		                fo_folders
		            WHERE
		                fo_workspace_id = $workspaceId
		                AND fo_parent_folder_id IS NULL
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
                name: "top_folder_content.get_folders")
		    .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$creatorIdentityType", userIdentity.IdentityType)
            .WithParameter("$creatorIdentity", userIdentity.Identity)
            .Execute();
    }
}