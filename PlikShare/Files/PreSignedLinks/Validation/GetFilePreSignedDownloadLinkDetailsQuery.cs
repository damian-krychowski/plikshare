using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;
using PlikShare.Files.Records;

namespace PlikShare.Files.PreSignedLinks.Validation;

public class GetFilePreSignedDownloadLinkDetailsQuery(PlikShareDb plikShareDb)
{
    public enum ResultCode
    {
        Ok = 0,
        NotFound
    }

    public Result Execute(
        FileExtId fileExternalId,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: """
                   SELECT
                        fi.fi_name,
                        fi.fi_content_type,
                        fi.fi_extension,
                        fi.fi_s3_key_secret_part,
                        fi.fi_size_in_bytes,
                        fi.fi_workspace_id,
                        fi.fi_encryption_key_version,
                        fi.fi_encryption_salt,
                        fi.fi_encryption_nonce_prefix,
                        fi.fi_encryption_chain_salts,
                        fi.fi_encryption_format_version,
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
                    WHERE fi.fi_external_id = $externalId
                    LIMIT 1
                """,
                readRowFunc: reader =>
                {
                    var encryptionKeyVersion = reader.GetByteOrNull(6);

                    return new FileRecord
                    {
                        ExternalId = fileExternalId,
                        Name = reader.DecodeEncryptableString(0, workspaceEncryptionSession),
                        ContentType = reader.DecodeEncryptableString(1, workspaceEncryptionSession),
                        Extension = reader.DecodeEncryptableString(2, workspaceEncryptionSession),
                        S3KeySecretPart = reader.GetString(3),
                        SizeInBytes = reader.GetInt64(4),
                        WorkspaceId = reader.GetInt32(5),
                        EncryptionMetadata = encryptionKeyVersion is null
                            ? null
                            : new FileEncryptionMetadata
                            {
                                KeyVersion = encryptionKeyVersion.Value,
                                Salt = reader.GetFieldValue<byte[]>(7),
                                NoncePrefix = reader.GetFieldValue<byte[]>(8),
                                ChainStepSalts = KeyDerivationChain.Deserialize(
                                    reader.GetFieldValueOrNull<byte[]>(9)),
                                FormatVersion = reader.GetByteOrNull(10) ?? 1
                            },
                        FolderAncestors = reader.GetFromJsonOrNull<FileRecordFolderAncestor[]>(11, workspaceEncryptionSession) ?? []
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
