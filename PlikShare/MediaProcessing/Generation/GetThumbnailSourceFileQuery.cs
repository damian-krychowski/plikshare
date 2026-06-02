using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;

namespace PlikShare.MediaProcessing.Generation;

public class GetThumbnailSourceFileQuery(
    PlikShareDb plikShareDb)
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
                        fi_workspace_id,
                        fi_key_secret_part,
                        fi_size_in_bytes,
                        fi_extension,
                        fi_encryption_key_version,
                        fi_encryption_salt,
                        fi_encryption_nonce_prefix,
                        fi_encryption_chain_salts,
                        fi_encryption_format_version
                    FROM fi_files
                    WHERE fi_external_id = $externalId
                      AND fi_deleted_at IS NULL
                    LIMIT 1
                """,
                readRowFunc: reader =>
                {
                    var encryptionKeyVersion = reader.GetByteOrNull(4);

                    return new ThumbnailSourceFileWithExtensions
                    {
                        ExternalId = fileExternalId,
                        WorkspaceId = reader.GetInt32(0),
                        KeySecretPart = reader.GetString(1),
                        SizeInBytes = reader.GetInt64(2),
                        Extension = reader.DecodeEncryptableString(3, workspaceEncryptionSession),
                        EncryptionMetadata = encryptionKeyVersion is null
                            ? null
                            : new FileEncryptionMetadata
                            {
                                KeyVersion = encryptionKeyVersion.Value,
                                Salt = reader.GetFieldValue<byte[]>(6),
                                NoncePrefix = reader.GetFieldValue<byte[]>(7),
                                ChainStepSalts = KeyDerivationChain.Deserialize(
                                    reader.GetFieldValueOrNull<byte[]>(8)),
                                FormatVersion = reader.GetByteOrNull(9) ?? 1
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

    public List<ThumbnailSourceFile> ExecuteBatch(
        List<string> fileExternalIds)
    {
        if (fileExternalIds.Count == 0)
            return [];

        using var connection = plikShareDb.OpenConnection();

        return connection
            .AggregateRows(
                sql: """
                    SELECT
                        fi_external_id,
                        fi_workspace_id,
                        fi_key_secret_part,
                        fi_size_in_bytes,
                        fi_encryption_key_version,
                        fi_encryption_salt,
                        fi_encryption_nonce_prefix,
                        fi_encryption_chain_salts,
                        fi_encryption_format_version
                    FROM fi_files
                    WHERE fi_external_id IN (
                            SELECT value FROM json_each($externalIds)
                        )
                      AND fi_deleted_at IS NULL
                """,
                seed: new List<ThumbnailSourceFile>(fileExternalIds.Count),
                aggregateRowFunc: (acc, reader) =>
                {
                    acc.Add(new ThumbnailSourceFile
                    {
                        ExternalId = reader.GetExtId<FileExtId>(0),
                        WorkspaceId = reader.GetInt32(1),
                        KeySecretPart = reader.GetString(2),
                        SizeInBytes = reader.GetInt64(3),

                        EncryptionMetadata = reader.GetByteOrNull(4) is { } keyVersion
                            ? new FileEncryptionMetadata
                            {
                                KeyVersion = keyVersion,
                                Salt = reader.GetFieldValue<byte[]>(5),
                                NoncePrefix = reader.GetFieldValue<byte[]>(6),
                                ChainStepSalts = KeyDerivationChain.Deserialize(
                                    reader.GetFieldValueOrNull<byte[]>(7)),
                                FormatVersion = reader.GetByteOrNull(8) ?? 1
                            }
                            : null
                    });

                    return acc;
                })
            .WithJsonParameter("$externalIds", fileExternalIds)
            .Execute();
    }

    public List<ThumbnailSourceFileWithExtensions> ExecuteBatch(
        List<string> fileExternalIds,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        if (fileExternalIds.Count == 0)
            return [];

        using var connection = plikShareDb.OpenConnection();

        return connection
            .AggregateRows(
                sql: """
                    SELECT
                        fi_external_id,
                        fi_workspace_id,
                        fi_key_secret_part,
                        fi_size_in_bytes,
                        fi_extension,
                        fi_encryption_key_version,
                        fi_encryption_salt,
                        fi_encryption_nonce_prefix,
                        fi_encryption_chain_salts,
                        fi_encryption_format_version
                    FROM fi_files
                    WHERE fi_external_id IN (
                            SELECT value FROM json_each($externalIds)
                        )
                      AND fi_deleted_at IS NULL
                """,
                seed: new List<ThumbnailSourceFileWithExtensions>(fileExternalIds.Count),
                aggregateRowFunc: (acc, reader) =>
                {
                    var extension = reader.DecodeEncryptableString(
                        4,
                        workspaceEncryptionSession);

                    if (!ContentTypeHelper.IsThumbnailable(extension))
                        return acc;

                    acc.Add(new ThumbnailSourceFileWithExtensions
                    {
                        ExternalId = reader.GetExtId<FileExtId>(0),
                        WorkspaceId = reader.GetInt32(1),
                        KeySecretPart = reader.GetString(2),
                        SizeInBytes = reader.GetInt64(3),
                        Extension = extension,

                        EncryptionMetadata = reader.GetByteOrNull(5) is { } keyVersion
                            ? new FileEncryptionMetadata
                            {
                                KeyVersion = keyVersion,
                                Salt = reader.GetFieldValue<byte[]>(6),
                                NoncePrefix = reader.GetFieldValue<byte[]>(7),
                                ChainStepSalts = KeyDerivationChain.Deserialize(
                                    reader.GetFieldValueOrNull<byte[]>(8)),
                                FormatVersion = reader.GetByteOrNull(9) ?? 1
                            }
                            : null
                    });

                    return acc;
                })
            .WithJsonParameter("$externalIds", fileExternalIds)
            .Execute();
    }

    public sealed record ThumbnailSourceFileWithExtensions
    {
        public required FileExtId ExternalId { get; init; }
        public required int WorkspaceId { get; init; }
        public required string KeySecretPart { get; init; }
        public required long SizeInBytes { get; init; }
        public required string Extension { get; init; }
        public required FileEncryptionMetadata? EncryptionMetadata { get; init; }
    }

    public sealed record ThumbnailSourceFile
    {
        public required FileExtId ExternalId { get; init; }
        public required int WorkspaceId { get; init; }
        public required string KeySecretPart { get; init; }
        public required long SizeInBytes { get; init; }
        public required FileEncryptionMetadata? EncryptionMetadata { get; init; }
    }

    public readonly record struct Result(
        ResultCode Code,
        ThumbnailSourceFileWithExtensions? Details = null);
}
