using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;
using PlikShare.Files.Records;
using PlikShare.Storages;
using PlikShare.Storages.Encryption;

namespace PlikShare.Files.Download;

public class GetFileDetailsQuery(PlikShareDb plikShareDb)
{
    public SQLiteOneRowCommandResult<FileRecord> Execute(
        int workspaceId,
        FileExtId fileExternalId,
        int? boxFolderId)
    {
        using var connection = plikShareDb.OpenConnection();

        return connection
            .OneRowCmd(
                sql: @"
                    SELECT
                        fi.fi_name,
                        fi.fi_content_type,
                        fi.fi_extension,
                        fi.fi_s3_key_secret_part,
                        fi.fi_size_in_bytes,
                        fi.fi_encryption_key_version,
                        fi.fi_encryption_salt,
                        fi.fi_encryption_nonce_prefix,
                        (
                            SELECT json_group_array(json_object(
                                'name', sub.fo_name,
                                'externalId', sub.fo_external_id
                            ))
                            FROM (
                                SELECT af.fo_name, af.fo_external_id
                                FROM fo_folders AS af
                                WHERE af.fo_id IN (SELECT value FROM json_each(f.fo_ancestor_folder_ids))
                                    OR af.fo_id = fi.fi_folder_id
                                ORDER BY json_array_length(af.fo_ancestor_folder_ids)
                            ) AS sub
                        )
                    FROM fi_files AS fi
                    LEFT JOIN fo_folders AS f ON fi.fi_folder_id = f.fo_id
                    WHERE
                        fi.fi_workspace_id = $workspaceId
                        AND fi.fi_external_id = $fileExternalId
                        AND (
                            $boxFolderId IS NULL
                            OR EXISTS (
                                SELECT 1
                                FROM fo_folders
                                WHERE
                                    fo_workspace_id = $workspaceId
                                    AND fo_id = fi.fi_folder_id
                                    AND fo_is_being_deleted = FALSE
                                    AND (
                                        fo_id = $boxFolderId
                                        OR $boxFolderId IN (
                                            SELECT value FROM json_each(fo_ancestor_folder_ids)
                                        )
                                    )
                            )
                        )
                ",
                readRowFunc: reader =>
                {
                    var encryptionKeyVersion = reader.GetByteOrNull(5);

                    return new FileRecord
                    {
                        ExternalId = fileExternalId,
                        Name = reader.GetString(0),
                        ContentType = reader.GetString(1),
                        Extension = reader.GetString(2),
                        S3KeySecretPart = reader.GetString(3),
                        SizeInBytes = reader.GetInt64(4),
                        WorkspaceId = workspaceId,
                        Encryption = encryptionKeyVersion is null
                            ? new FileEncryption
                            {
                                EncryptionType = StorageEncryptionType.None
                            }
                            : new FileEncryption
                            {
                                EncryptionType = StorageEncryptionType.Managed,
                                Metadata = new FileEncryptionMetadata
                                {
                                    KeyVersion = encryptionKeyVersion.Value,
                                    Salt = reader.GetFieldValue<byte[]>(6),
                                    NoncePrefix = reader.GetFieldValue<byte[]>(7)
                                }
                            },
                        FolderAncestors = reader.GetFromJsonOrNull<FileRecordFolderAncestor[]>(8) ?? []
                    };
                })
            .WithParameter("$workspaceId", workspaceId)
            .WithParameter("$fileExternalId", fileExternalId.Value)
            .WithParameter("$boxFolderId", boxFolderId)
            .Execute();
    }
}
