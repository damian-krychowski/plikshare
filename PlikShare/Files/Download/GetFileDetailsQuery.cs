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
                        fi_name,
                        fi_content_type,
                        fi_extension,
                        fi_s3_key_secret_part,
                        fi_size_in_bytes,
                        fi_encryption_key_version,
                        fi_encryption_salt,
                        fi_encryption_nonce_prefix
                    FROM fi_files
                    WHERE
                        fi_workspace_id = $workspaceId
                        AND fi_external_id = $fileExternalId
                        AND (
                            $boxFolderId IS NULL 
                            OR EXISTS (
                                SELECT 1
                                FROM fo_folders
                                WHERE 
                                    fo_workspace_id = $workspaceId
                                    AND fo_id = fi_folder_id
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
                            }
                    };
                })
            .WithParameter("$workspaceId", workspaceId)
            .WithParameter("$fileExternalId", fileExternalId.Value)
            .WithParameter("$boxFolderId", boxFolderId)
            .Execute();
    }
}