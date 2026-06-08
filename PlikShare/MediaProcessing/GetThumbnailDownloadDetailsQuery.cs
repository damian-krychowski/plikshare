using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Files.Metadata;
using PlikShare.Files.Records;
using PlikShare.Workspaces.Cache;

namespace PlikShare.MediaProcessing;

// Resolves the download details (key, size, content type, encryption metadata) of a parent
// file's thumbnail child for a given variant. The variant lives in the encrypted fi_metadata,
// so we decrypt app-side and pick the newest child whose decoded metadata matches — same
// pooled-connection pattern as GetThumbnailsQuery. Returns null when the parent has no such
// thumbnail (or the parent doesn't exist / is deleted).
public class GetThumbnailDownloadDetailsQuery(PlikShareDb plikShareDb)
{
    public sealed record Result(FileRecord File, string Etag);

    public Result? Execute(
        WorkspaceContext workspace,
        FileExtId parentFileExternalId,
        ThumbnailVariant variant,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        int? boxFolderId = null)
    {
        using var connection = plikShareDb.OpenConnection();

        return connection
            .AggregateRowsUntil(
                sql: @"
                    SELECT
                        child_fi.fi_external_id,
                        child_fi.fi_name,
                        child_fi.fi_content_type,
                        child_fi.fi_extension,
                        child_fi.fi_key_secret_part,
                        child_fi.fi_size_in_bytes,
                        child_fi.fi_workspace_id,
                        child_fi.fi_encryption_key_version,
                        child_fi.fi_encryption_salt,
                        child_fi.fi_encryption_nonce_prefix,
                        child_fi.fi_encryption_chain_salts,
                        child_fi.fi_encryption_format_version,
                        child_fi.fi_metadata
                    FROM fi_files AS child_fi
                    INNER JOIN fi_files AS parent_fi
                        ON parent_fi.fi_id = child_fi.fi_parent_file_id
                    WHERE
                        parent_fi.fi_external_id = $parentExternalId
                        AND parent_fi.fi_workspace_id = $workspaceId
                        AND parent_fi.fi_deleted_at IS NULL
                        AND child_fi.fi_workspace_id = $workspaceId
                        AND child_fi.fi_deleted_at IS NULL
                        AND child_fi.fi_is_upload_completed = TRUE
                        AND child_fi.fi_metadata IS NOT NULL
                        AND (
                            $boxFolderId IS NULL
                            OR EXISTS (
                                SELECT 1
                                FROM fo_folders
                                WHERE
                                    fo_workspace_id = $workspaceId
                                    AND fo_id = parent_fi.fi_folder_id
                                    AND fo_is_being_deleted = FALSE
                                    AND (
                                        fo_id = $boxFolderId
                                        OR $boxFolderId IN (
                                            SELECT value FROM json_each(fo_ancestor_folder_ids)
                                        )
                                    )
                            )
                        )
                    ORDER BY child_fi.fi_id DESC
                ",
                seed: (Result?)null,
                aggregateRowFunc: (acc, reader) =>
                {
                    var metadataJson = reader.DecodeEncryptableBlobOrNull(
                        12,
                        workspaceEncryptionSession);

                    if (metadataJson is null)
                        return (acc, false);

                    if (Json.Deserialize<FileMetadata>(metadataJson)
                        is not ThumbnailFileMetadata thumbnailMetadata
                        || thumbnailMetadata.Variant != variant)
                    {
                        return (acc, false);
                    }

                    var encryptionKeyVersion = reader.GetByteOrNull(7);

                    var file = new FileRecord
                    {
                        ExternalId = reader.GetExtId<FileExtId>(0),
                        Name = reader.DecodeEncryptableString(1, workspaceEncryptionSession),
                        ContentType = reader.DecodeEncryptableString(2, workspaceEncryptionSession),
                        Extension = reader.DecodeEncryptableString(3, workspaceEncryptionSession),
                        KeySecretPart = reader.GetString(4),
                        SizeInBytes = reader.GetInt64(5),
                        WorkspaceId = reader.GetInt32(6),
                        EncryptionMetadata = encryptionKeyVersion is null
                            ? null
                            : new FileEncryptionMetadata
                            {
                                KeyVersion = encryptionKeyVersion.Value,
                                Salt = reader.GetFieldValue<byte[]>(8),
                                NoncePrefix = reader.GetFieldValue<byte[]>(9),
                                ChainStepSalts = KeyDerivationChain.Deserialize(
                                    reader.GetFieldValueOrNull<byte[]>(10)),
                                FormatVersion = reader.GetByteOrNull(11) ?? 1
                            },
                        FolderAncestors = []
                    };

                    return (new Result(file, thumbnailMetadata.Etag), true);
                },
                name: "download.thumbnail_details")
            .WithParameter("$parentExternalId", parentFileExternalId.Value)
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$boxFolderId", boxFolderId)
            .Execute();
    }
}
