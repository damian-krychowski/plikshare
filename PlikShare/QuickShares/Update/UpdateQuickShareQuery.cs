using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.QuickShares.Cache;
using Serilog;

namespace PlikShare.QuickShares.Update;

public class UpdateQuickShareQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        QuickShareContext quickShare,
        bool updateName,
        string? name,
        bool updateExpiration,
        DateTimeOffset? expiresAt,
        bool updateMaxDownloads,
        int? maxDownloads,
        bool updatePassword,
        string? passwordHashBase64,
        byte[]? passwordSalt,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                quickShare: quickShare,
                updateName: updateName,
                name: name,
                updateExpiration: updateExpiration,
                expiresAt: expiresAt,
                updateMaxDownloads: updateMaxDownloads,
                maxDownloads: maxDownloads,
                updatePassword: updatePassword,
                passwordHashBase64: passwordHashBase64,
                passwordSalt: passwordSalt),
            cancellationToken: cancellationToken);
    }

    private static ResultCode ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        QuickShareContext quickShare,
        bool updateName,
        string? name,
        bool updateExpiration,
        DateTimeOffset? expiresAt,
        bool updateMaxDownloads,
        int? maxDownloads,
        bool updatePassword,
        string? passwordHashBase64,
        byte[]? passwordSalt)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE qsh_quick_shares
                     SET qsh_name          = CASE WHEN $updateName          THEN $name         ELSE qsh_name          END,
                         qsh_expires_at    = CASE WHEN $updateExpiration    THEN $expiresAt    ELSE qsh_expires_at    END,
                         qsh_max_downloads = CASE WHEN $updateMaxDownloads  THEN $maxDownloads ELSE qsh_max_downloads END,
                         qsh_password_hash = CASE WHEN $updatePassword      THEN $passwordHash ELSE qsh_password_hash END,
                         qsh_password_salt = CASE WHEN $updatePassword      THEN $passwordSalt ELSE qsh_password_salt END
                     WHERE qsh_id = $quickShareId
                     RETURNING qsh_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$quickShareId", quickShare.Id)
            .WithParameter("$updateName", updateName)
            .WithParameter("$name", name)
            .WithParameter("$updateExpiration", updateExpiration)
            .WithParameter("$expiresAt", expiresAt)
            .WithParameter("$updateMaxDownloads", updateMaxDownloads)
            .WithParameter("$maxDownloads", maxDownloads)
            .WithParameter("$updatePassword", updatePassword)
            .WithParameter("$passwordHash", passwordHashBase64)
            .WithParameter("$passwordSalt", passwordSalt)
            .Execute();

        if (result.IsEmpty)
        {
            Log.Warning(
                "Could not update QuickShare '{ExternalId}' because it was not found",
                quickShare.ExternalId);
            return ResultCode.NotFound;
        }

        Log.Information(
            "QuickShare '{ExternalId} ({Id})' updated (name={Name}, expiration={Expiration}, maxDownloads={MaxDownloads}, password={Password})",
            quickShare.ExternalId,
            result.Value,
            updateName,
            updateExpiration,
            updateMaxDownloads,
            updatePassword);

        return ResultCode.Ok;
    }

    public enum ResultCode
    {
        Ok = 0,
        NotFound
    }
}
