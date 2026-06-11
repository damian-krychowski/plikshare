using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Files.Id;
using PlikShare.Files.Metadata.Contracts;
using PlikShare.Folders.List.Contracts;
using PlikShare.MediaProcessing;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Files.Get;

public class GetFileQuery(PlikShareDb plikShareDb)
{
    public FileDto? Execute(
        WorkspaceContext workspace,
        FileExtId fileExternalId,
        IUserIdentity userIdentity,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: @"
                    SELECT
                        fi_external_id,
                        fi_name,
                        fi_extension,
                        fi_size_in_bytes,
                        (
                            fi_uploader_identity_type = $uploaderIdentityType
                            AND fi_uploader_identity = $uploaderIdentity
                        ) AS fi_was_uploaded_by_user,
                        NOT fi_is_upload_completed,
                        fi_position,
                        fi_created_at,
                        fi_metadata,
                        (
                            SELECT json_group_array(CAST(child_fi.fi_metadata AS TEXT))
                            FROM fi_files AS child_fi
                            WHERE
                                child_fi.fi_parent_file_id = fi_files.fi_id
                                AND child_fi.fi_workspace_id = $workspaceId
                                AND child_fi.fi_deleted_at IS NULL
                                AND child_fi.fi_is_upload_completed = TRUE
                                AND child_fi.fi_metadata IS NOT NULL
                        ) AS fi_children_metadata
                    FROM fi_files
                    WHERE
                        fi_external_id = $fileExternalId
                        AND fi_workspace_id = $workspaceId
                        AND fi_parent_file_id IS NULL
                        AND fi_deleted_at IS NULL
                ",
                readRowFunc: reader => new FileDto
                {
                    ExternalId = reader.GetString(0),
                    Name = reader.DecodeEncryptableString(1, workspaceEncryptionSession),
                    Extension = reader.DecodeEncryptableString(2, workspaceEncryptionSession),
                    SizeInBytes = reader.GetInt64(3),
                    WasUploadedByUser = reader.GetBoolean(4),
                    IsLocked = reader.GetBoolean(5),
                    Position = reader.GetInt64OrNull(6) ?? 0,
                    CreatedAt = reader.GetDateTimeOffsetOrNull(7)?.UtcDateTime,
                    Metadata = FileMetadataFactory.Prepare(
                        thumbnail: ThumbnailEtagsMetadata.PrepareDto(
                            reader.GetFromJsonOrNull<List<string>>(9),
                            workspaceEncryptionSession),
                        dimensions: ImageDimensionsMetadata.Read(reader, 8, workspaceEncryptionSession) is { } dimensions
                            ? new DimensionsMetadataDto { Width = dimensions.Width, Height = dimensions.Height }
                            : null)
                },
                name: "file.get")
            .WithParameter("$fileExternalId", fileExternalId.Value)
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$uploaderIdentityType", userIdentity.IdentityType)
            .WithParameter("$uploaderIdentity", userIdentity.Identity)
            .Execute();

        return result.IsEmpty
            ? null
            : result.Value;
    }
}
