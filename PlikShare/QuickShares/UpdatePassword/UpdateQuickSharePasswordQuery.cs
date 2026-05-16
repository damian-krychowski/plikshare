using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.QuickShares.Cache;
using Serilog;

namespace PlikShare.QuickShares.UpdatePassword;

public class UpdateQuickSharePasswordQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        QuickShareContext quickShare,
        string? passwordHashBase64,
        byte[]? passwordSalt,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                quickShare: quickShare,
                passwordHashBase64: passwordHashBase64,
                passwordSalt: passwordSalt),
            cancellationToken: cancellationToken);
    }

    private static ResultCode ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        QuickShareContext quickShare,
        string? passwordHashBase64,
        byte[]? passwordSalt)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE qsh_quick_shares
                     SET qsh_password_hash = $passwordHash,
                         qsh_password_salt = $passwordSalt
                     WHERE qsh_id = $quickShareId
                     RETURNING qsh_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$quickShareId", quickShare.Id)
            .WithParameter("$passwordHash", passwordHashBase64)
            .WithParameter("$passwordSalt", passwordSalt)
            .Execute();

        if (result.IsEmpty)
        {
            Log.Warning(
                "Could not update QuickShare '{ExternalId}' password because it was not found",
                quickShare.ExternalId);
            return ResultCode.NotFound;
        }

        Log.Information(
            "QuickShare '{ExternalId} ({Id})' password updated (set={WasSet})",
            quickShare.ExternalId,
            result.Value,
            passwordHashBase64 is not null);

        return ResultCode.Ok;
    }

    public enum ResultCode
    {
        Ok = 0,
        NotFound
    }
}
