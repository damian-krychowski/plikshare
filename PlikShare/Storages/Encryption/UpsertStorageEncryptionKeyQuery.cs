using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Storages.Encryption;

/// <summary>
/// Inserts or replaces a single user's wrap of a specific Storage DEK version in
/// <c>sek_storage_encryption_keys</c>. Conflict key is (storage_id, user_id, version), so a
/// rotation flow can insert a new version alongside older ones without touching previous
/// rows; granting access to a new admin inserts the same version they are being given.
/// </summary>
public class UpsertStorageEncryptionKeyQuery(
    DbWriteQueue dbWriteQueue,
    IClock clock)
{
    public Task Execute(
        int storageId,
        int userId,
        int storageDekVersion,
        byte[] wrappedStorageDek,
        int? wrappedByUserId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                storageId: storageId,
                userId: userId,
                storageDekVersion: storageDekVersion,
                wrappedStorageDek: wrappedStorageDek,
                wrappedByUserId: wrappedByUserId),
            cancellationToken: cancellationToken);
    }

    public void ExecuteTransaction(
        SqliteWriteContext dbWriteContext,
        int storageId,
        int userId,
        int storageDekVersion,
        byte[] wrappedStorageDek,
        int? wrappedByUserId,
        Microsoft.Data.Sqlite.SqliteTransaction transaction)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     INSERT INTO sek_storage_encryption_keys(
                         sek_storage_id,
                         sek_user_id,
                         sek_storage_dek_version,
                         sek_wrapped_storage_dek,
                         sek_wrapped_at,
                         sek_wrapped_by_user_id
                     ) VALUES (
                         $storageId,
                         $userId,
                         $version,
                         $wrappedStorageDek,
                         $wrappedAt,
                         $wrappedByUserId
                     )
                     ON CONFLICT(sek_storage_id, sek_user_id, sek_storage_dek_version) DO UPDATE SET
                         sek_wrapped_storage_dek = excluded.sek_wrapped_storage_dek,
                         sek_wrapped_at = excluded.sek_wrapped_at,
                         sek_wrapped_by_user_id = excluded.sek_wrapped_by_user_id
                     RETURNING sek_user_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$storageId", storageId)
            .WithParameter("$userId", userId)
            .WithParameter("$version", storageDekVersion)
            .WithParameter("$wrappedStorageDek", wrappedStorageDek)
            .WithParameter("$wrappedAt", clock.UtcNow)
            .WithParameter("$wrappedByUserId", (object?)wrappedByUserId ?? DBNull.Value)
            .Execute();

        if (result.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Failed to upsert storage encryption key for User '{userId}' on Storage '{storageId}' v{storageDekVersion}.");
        }

        Log.Information(
            "Storage#{StorageId} encryption key v{Version} was wrapped for User#{UserId} (by User#{WrappedByUserId}).",
            storageId, storageDekVersion, userId, wrappedByUserId);
    }

    private void ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        int storageId,
        int userId,
        int storageDekVersion,
        byte[] wrappedStorageDek,
        int? wrappedByUserId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            ExecuteTransaction(
                dbWriteContext: dbWriteContext,
                storageId: storageId,
                userId: userId,
                storageDekVersion: storageDekVersion,
                wrappedStorageDek: wrappedStorageDek,
                wrappedByUserId: wrappedByUserId,
                transaction: transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
