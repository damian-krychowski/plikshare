using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;
using PlikShare.Files.Records;

namespace PlikShare.MediaProcessing.Generation;

/// <summary>
/// Minimal lookup of the parent file's storage-download essentials for a thumbnail generation
/// job. Returns only the four fields the executor actually consumes to open the source stream —
/// <see cref="GetFilePreSignedDownloadLinkDetailsQuery"/> additionally fetches name, content type,
/// extension and the folder-ancestor JSON tree (an expensive subquery), none of which this path
/// needs. The batched <see cref="ExecuteBatch"/> resolves N files in one SQL round-trip.
/// </summary>
public class GetThumbnailSourceFileQuery(PlikShareDb plikShareDb)
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
                sql: """
                    SELECT
                        fi.fi_workspace_id,
                        fi.fi_key_secret_part,
                        fi.fi_size_in_bytes,
                        fi.fi_encryption_key_version,
                        fi.fi_encryption_salt,
                        fi.fi_encryption_nonce_prefix,
                        fi.fi_encryption_chain_salts,
                        fi.fi_encryption_format_version
                    FROM fi_files AS fi
                    WHERE fi.fi_external_id = $externalId
                      AND fi.fi_deleted_at IS NULL
                    LIMIT 1
                """,
                readRowFunc: reader =>
                {
                    var encryptionKeyVersion = reader.GetByteOrNull(3);

                    return new ThumbnailSourceFile
                    {
                        WorkspaceId = reader.GetInt32(0),
                        KeySecretPart = reader.GetString(1),
                        SizeInBytes = reader.GetInt64(2),
                        EncryptionMetadata = encryptionKeyVersion is null
                            ? null
                            : new FileEncryptionMetadata
                            {
                                KeyVersion = encryptionKeyVersion.Value,
                                Salt = reader.GetFieldValue<byte[]>(4),
                                NoncePrefix = reader.GetFieldValue<byte[]>(5),
                                ChainStepSalts = KeyDerivationChain.Deserialize(
                                    reader.GetFieldValueOrNull<byte[]>(6)),
                                FormatVersion = reader.GetByteOrNull(7) ?? 1
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

    /// <summary>
    /// Batch variant: resolves the parents of every <see cref="FileExtId"/> in one SQL query
    /// (<c>WHERE fi_external_id IN (json_each($ids))</c>). Returns a map keyed by external id;
    /// missing entries indicate a deleted-or-never-existed parent (race), to be handled by the
    /// caller per file.
    /// </summary>
    public Dictionary<FileExtId, ThumbnailSourceFile> ExecuteBatch(
        IReadOnlyList<FileExtId> fileExternalIds)
    {
        if (fileExternalIds.Count == 0)
            return [];

        using var connection = plikShareDb.OpenConnection();

        return connection
            .AggregateRows(
                sql: """
                    SELECT
                        fi.fi_external_id,
                        fi.fi_workspace_id,
                        fi.fi_key_secret_part,
                        fi.fi_size_in_bytes,
                        fi.fi_encryption_key_version,
                        fi.fi_encryption_salt,
                        fi.fi_encryption_nonce_prefix,
                        fi.fi_encryption_chain_salts,
                        fi.fi_encryption_format_version
                    FROM fi_files AS fi
                    WHERE fi.fi_external_id IN (
                            SELECT value FROM json_each($externalIds)
                        )
                      AND fi.fi_deleted_at IS NULL
                """,
                seed: new Dictionary<FileExtId, ThumbnailSourceFile>(fileExternalIds.Count),
                aggregateRowFunc: (acc, reader) =>
                {
                    var externalId = reader.GetExtId<FileExtId>(0);

                    var source = new ThumbnailSourceFile
                    {
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
                    };

                    acc[externalId] = source;
                    return acc;
                })
            .WithJsonParameter("$externalIds", fileExternalIds.Select(id => id.Value).ToList())
            .Execute();
    }

    public sealed record ThumbnailSourceFile
    {
        public required int WorkspaceId { get; init; }
        public required string KeySecretPart { get; init; }
        public required long SizeInBytes { get; init; }
        public required FileEncryptionMetadata? EncryptionMetadata { get; init; }
    }

    public readonly record struct Result(
        ResultCode Code,
        ThumbnailSourceFile? Details = default);
}
