using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.Storages.Encryption;

/// <summary>
/// Reads the per-user wrap of a Storage DEK from sek_storage_encryption_keys.
/// Returns the ciphertext blob the caller can unwrap with their private key, or null
/// when the user has no access to that storage.
/// </summary>
public class StorageEncryptionKeyReader(PlikShareDb plikShareDb)
{
    public byte[]? TryLoadWrappedDek(int storageId, int userId)
    {
        using var connection = plikShareDb.OpenConnection();

        var (isEmpty, wrappedDek) = connection
            .OneRowCmd(
                sql: """
                     SELECT sek_wrapped_storage_dek
                     FROM sek_storage_encryption_keys
                     WHERE sek_storage_id = $storageId
                       AND sek_user_id = $userId
                     LIMIT 1
                     """,
                readRowFunc: reader => reader.GetFieldValue<byte[]>(0))
            .WithParameter("$storageId", storageId)
            .WithParameter("$userId", userId)
            .Execute();

        return isEmpty ? null : wrappedDek;
    }

    public List<int> ListStorageAdminIds(int storageId)
    {
        using var connection = plikShareDb.OpenConnection();

        return connection
            .Cmd(
                sql: """
                     SELECT sek_user_id
                     FROM sek_storage_encryption_keys
                     WHERE sek_storage_id = $storageId
                     ORDER BY sek_user_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$storageId", storageId)
            .Execute();
    }
}
