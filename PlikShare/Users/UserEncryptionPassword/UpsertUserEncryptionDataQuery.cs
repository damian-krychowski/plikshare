using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Users.UserEncryptionPassword;

/// <summary>
/// Atomically writes the full encryption state for a user to u_users. Used by both the
/// initial setup (writes all seven columns from scratch) and change/reset flows
/// (rewrites the columns that change; public key and recovery-wrapped key carry over
/// unchanged by the caller re-supplying them).
/// </summary>
public class UpsertUserEncryptionDataQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        int userId,
        byte[] publicKey,
        byte[] encryptedPrivateKey,
        byte[] kdfSalt,
        EncryptionPasswordKdf.Params kdfParams,
        byte[] verifyHash,
        byte[] recoveryWrappedPrivateKey,
        byte[] recoveryVerifyHash,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                userId: userId,
                publicKey: publicKey,
                encryptedPrivateKey: encryptedPrivateKey,
                kdfSalt: kdfSalt,
                kdfParams: kdfParams,
                verifyHash: verifyHash,
                recoveryWrappedPrivateKey: recoveryWrappedPrivateKey,
                recoveryVerifyHash: recoveryVerifyHash),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        int userId,
        byte[] publicKey,
        byte[] encryptedPrivateKey,
        byte[] kdfSalt,
        EncryptionPasswordKdf.Params kdfParams,
        byte[] verifyHash,
        byte[] recoveryWrappedPrivateKey,
        byte[] recoveryVerifyHash)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE u_users
                     SET
                         u_encryption_public_key = $publicKey,
                         u_encryption_encrypted_private_key = $encryptedPrivateKey,
                         u_encryption_kdf_salt = $kdfSalt,
                         u_encryption_kdf_params = $kdfParams,
                         u_encryption_verify_hash = $verifyHash,
                         u_encryption_recovery_wrapped_private_key = $recoveryWrappedPrivateKey,
                         u_encryption_recovery_verify_hash = $recoveryVerifyHash,
                         u_concurrency_stamp = $concurrencyStamp
                     WHERE u_id = $userId
                     RETURNING u_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$userId", userId)
            .WithParameter("$publicKey", publicKey)
            .WithParameter("$encryptedPrivateKey", encryptedPrivateKey)
            .WithParameter("$kdfSalt", kdfSalt)
            .WithParameter("$kdfParams", EncryptionPasswordKdf.SerializeParams(kdfParams))
            .WithParameter("$verifyHash", verifyHash)
            .WithParameter("$recoveryWrappedPrivateKey", recoveryWrappedPrivateKey)
            .WithParameter("$recoveryVerifyHash", recoveryVerifyHash)
            .WithParameter("$concurrencyStamp", Guid.NewGuid())
            .Execute();

        if (result.IsEmpty)
        {
            Log.Warning("Could not upsert encryption data for User '{UserId}' — user not found.", userId);
            return ResultCode.UserNotFound;
        }

        Log.Information("User '{UserId}' encryption data was updated.", userId);
        return ResultCode.Ok;
    }

    public enum ResultCode
    {
        Ok = 0,
        UserNotFound
    }
}
