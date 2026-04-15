using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Storages.Encryption;

/// <summary>
/// Removes a user's wrap of a Storage DEK, revoking their storage-admin access.
/// The DEK itself is unchanged — other admins keep their wraps. For forward secrecy
/// (preventing the revoked user from using a leaked copy of the DEK) a separate
/// key-rotation flow would be needed; this query only revokes current access.
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
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     DELETE FROM sek_storage_encryption_keys
                     WHERE sek_storage_id = $storageId
                       AND sek_user_id = $userId
                     RETURNING sek_user_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$storageId", storageId)
            .WithParameter("$userId", userId)
            .Execute();

        if (result.IsEmpty)
        {
            Log.Warning(
                "Revoke storage encryption key skipped — User#{UserId} has no wrap on Storage#{StorageId}.",
                userId, storageId);
            return ResultCode.NotFound;
        }

        Log.Information(
            "Storage#{StorageId} encryption key revoked from User#{UserId}.",
            storageId, userId);
        return ResultCode.Ok;
    }

    public enum ResultCode
    {
        Ok = 0,
        NotFound
    }
}
