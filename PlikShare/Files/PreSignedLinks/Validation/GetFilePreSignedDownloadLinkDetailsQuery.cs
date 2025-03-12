using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;
using PlikShare.Files.Records;
using PlikShare.Storages;
using PlikShare.Storages.Encryption;

namespace PlikShare.Files.PreSignedLinks.Validation;

public class GetFilePreSignedDownloadLinkDetailsQuery(PlikShareDb plikShareDb)
{
    public enum ResultCode
    {
        Ok = 0,
        NotFound
    }

    public Result Execute(
        FileExtId fileExternalId)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: @"
                    SELECT 
                        fi_name,
                        fi_content_type,
                        fi_extension,
                        fi_s3_key_secret_part,
                        fi_size_in_bytes,
                        fi_workspace_id,
                        fi_encryption_key_version,
                        fi_encryption_salt,
                        fi_encryption_nonce_prefix
                    FROM fi_files
                    WHERE fi_external_id = $externalId
                    LIMIT 1                        
                ",
                readRowFunc: reader =>
                {
                    var encryptionKeyVersion = reader.GetByteOrNull(6);

                    return new FileRecord
                    {
                        ExternalId = fileExternalId,
                        Name = reader.GetString(0),
                        ContentType = reader.GetString(1),
                        Extension = reader.GetString(2),
                        S3KeySecretPart = reader.GetString(3),
                        SizeInBytes = reader.GetInt64(4),
                        WorkspaceId = reader.GetInt32(5),
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
                                    Salt = reader.GetFieldValue<byte[]>(7),
                                    NoncePrefix = reader.GetFieldValue<byte[]>(8)
                                }
                            }
                    };
                })
            .WithParameter("$externalId", fileExternalId.Value)
            .Execute();

        if (result.IsEmpty)
            return new Result(Code: ResultCode.NotFound);

        return new Result(
            Code: ResultCode.Ok,
            Details: result.Value);
    }
    public record Result(
        ResultCode Code,
        FileRecord? Details = default);
}