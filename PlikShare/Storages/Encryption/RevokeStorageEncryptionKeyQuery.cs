using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Storages.Encryption;

/// <summary>
/// Removes ALL wraps of Storage DEKs owned by this user for this storage, across every
/// Storage DEK version they held. The DEKs themselves are unchanged — other admins keep
/// their wraps. Forward secrecy (preventing a revoked user from using a previously-leaked
/// plaintext copy of a DEK) still needs a separate key-rotation flow; this query only
/// revokes current access.
/// </summary>
public class RevokeStorageEncryptionKeyQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        int storageId,
        int userId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                storageId: storageId,
                userId: userId),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        int storageId,
        int userId)
    {
        var removedVersions = dbWriteContext
            .Cmd(
                sql: """
                     DELETE FROM sek_storage_encryption_keys
                     WHERE sek_storage_id = $storageId
                       AND sek_user_id = $userId
                     RETURNING sek_storage_dek_version
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$storageId", storageId)
            .WithParameter("$userId", userId)
            .Execute();

        if (removedVersions.Count == 0)
        {
            Log.Warning(
                "Revoke storage encryption key skipped — User#{UserId} has no wrap on Storage#{StorageId}.",
                userId, storageId);
            return ResultCode.NotFound;
        }

        Log.Information(
            "Storage#{StorageId} encryption key versions [{Versions}] revoked from User#{UserId}.",
            storageId, removedVersions, userId);
        return ResultCode.Ok;
    }

    public enum ResultCode
    {
        Ok = 0,
        NotFound
    }
}
