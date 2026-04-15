using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Storages.Encryption;

/// <summary>
/// Inserts or replaces a single user's wrap of a Storage DEK in sek_storage_encryption_keys.
/// Used for: initial wrap at storage creation (creator wraps DEK for themselves), granting
/// storage-admin access to another user (existing admin wraps DEK for invitee), and rewrap
/// on key rotation (same user, new wrap).
/// </summary>
public class UpsertStorageEncryptionKeyQuery(
    DbWriteQueue dbWriteQueue,
    IClock clock)
{
    public Task Execute(
        int storageId,
        int userId,
        byte[] wrappedStorageDek,
        int? wrappedByUserId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                storageId: storageId,
                userId: userId,
                wrappedStorageDek: wrappedStorageDek,
                wrappedByUserId: wrappedByUserId),
            cancellationToken: cancellationToken);
    }

    public void ExecuteTransaction(
        SqliteWriteContext dbWriteContext,
        int storageId,
        int userId,
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
                         sek_wrapped_storage_dek,
                         sek_wrapped_at,
                         sek_wrapped_by_user_id
                     ) VALUES (
                         $storageId,
                         $userId,
                         $wrappedStorageDek,
                         $wrappedAt,
                         $wrappedByUserId
                     )
                     ON CONFLICT(sek_storage_id, sek_user_id) DO UPDATE SET
                         sek_wrapped_storage_dek = excluded.sek_wrapped_storage_dek,
                         sek_wrapped_at = excluded.sek_wrapped_at,
                         sek_wrapped_by_user_id = excluded.sek_wrapped_by_user_id
                     RETURNING sek_user_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$storageId", storageId)
            .WithParameter("$userId", userId)
            .WithParameter("$wrappedStorageDek", wrappedStorageDek)
            .WithParameter("$wrappedAt", clock.UtcNow)
            .WithParameter("$wrappedByUserId", (object?)wrappedByUserId ?? DBNull.Value)
            .Execute();

        if (result.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Failed to upsert storage encryption key for User '{userId}' on Storage '{storageId}'.");
        }

        Log.Information(
            "Storage#{StorageId} encryption key was wrapped for User#{UserId} (by User#{WrappedByUserId}).",
            storageId, userId, wrappedByUserId);
    }

    private void ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        int storageId,
        int userId,
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
