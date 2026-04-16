using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.Storages.Encryption;

/// <summary>
/// Reads the per-user per-version wrap of a Storage DEK from
/// <c>sek_storage_encryption_keys</c>. Each storage may have several Storage DEK versions
/// (rotation appends new ones), and each storage admin holds a sealed-box wrap per version
/// they still have access to. Callers resolve "latest" by asking for MAX(version); there is
/// no denormalised current-version pointer.
/// </summary>
public class StorageEncryptionKeyReader(PlikShareDb plikShareDb)
{
    public byte[]? TryLoadWrappedDek(
        int storageId, 
        int userId, 
        int storageDekVersion)
    {
        using var connection = plikShareDb.OpenConnection();

        var (isEmpty, wrappedDek) = connection
            .OneRowCmd(
                sql: """
                     SELECT sek_wrapped_storage_dek
                     FROM sek_storage_encryption_keys
                     WHERE sek_storage_id = $storageId
                       AND sek_user_id = $userId
                       AND sek_storage_dek_version = $version
                     LIMIT 1
                     """,
                readRowFunc: reader => reader.GetFieldValue<byte[]>(0))
            .WithParameter("$storageId", storageId)
            .WithParameter("$userId", userId)
            .WithParameter("$version", storageDekVersion)
            .Execute();

        return isEmpty ? null : wrappedDek;
    }

    /// <summary>
    /// Returns the newest Storage DEK version this user holds a wrap for, together with the
    /// sealed-box ciphertext. Used by workspace creation to pick which Storage DEK to derive
    /// the new Workspace DEK from.
    /// </summary>
    public LatestWrappedDek? TryLoadLatestWrappedDek(int storageId, int userId)
    {
        using var connection = plikShareDb.OpenConnection();

        var (isEmpty, row) = connection
            .OneRowCmd(
                sql: """
                     SELECT sek_storage_dek_version, sek_wrapped_storage_dek
                     FROM sek_storage_encryption_keys
                     WHERE sek_storage_id = $storageId
                       AND sek_user_id = $userId
                     ORDER BY sek_storage_dek_version DESC
                     LIMIT 1
                     """,
                readRowFunc: reader => new LatestWrappedDek(
                    StorageDekVersion: reader.GetInt32(0),
                    WrappedDek: reader.GetFieldValue<byte[]>(1)))
            .WithParameter("$storageId", storageId)
            .WithParameter("$userId", userId)
            .Execute();

        return isEmpty ? null : row;
    }

    public List<int> ListStorageAdminIds(int storageId)
    {
        using var connection = plikShareDb.OpenConnection();

        return connection
            .Cmd(
                sql: """
                     SELECT DISTINCT sek_user_id
                     FROM sek_storage_encryption_keys
                     WHERE sek_storage_id = $storageId
                     ORDER BY sek_user_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$storageId", storageId)
            .Execute();
    }

    public readonly record struct LatestWrappedDek(
        int StorageDekVersion,
        byte[] WrappedDek);
}
