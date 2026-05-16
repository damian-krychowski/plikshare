using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.QuickShares.Cache;
using Serilog;

namespace PlikShare.QuickShares.UpdateExpiration;

public class UpdateQuickShareExpirationQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        QuickShareContext quickShare,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                quickShare: quickShare,
                expiresAt: expiresAt),
            cancellationToken: cancellationToken);
    }

    private static ResultCode ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        QuickShareContext quickShare,
        DateTimeOffset? expiresAt)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE qsh_quick_shares
                     SET qsh_expires_at = $expiresAt
                     WHERE qsh_id = $quickShareId
                     RETURNING qsh_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$quickShareId", quickShare.Id)
            .WithParameter("$expiresAt", expiresAt)
            .Execute();

        if (result.IsEmpty)
        {
            Log.Warning(
                "Could not update QuickShare '{ExternalId}' expiration to '{ExpiresAt}' because it was not found",
                quickShare.ExternalId,
                expiresAt);
            return ResultCode.NotFound;
        }

        Log.Information(
            "QuickShare '{ExternalId} ({Id})' expiration updated to '{ExpiresAt}'",
            quickShare.ExternalId,
            result.Value,
            expiresAt);

        return ResultCode.Ok;
    }

    public enum ResultCode
    {
        Ok = 0,
        NotFound
    }
}
