using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.QuickShares.Cache;
using Serilog;

namespace PlikShare.QuickShares.TrackDownload;

public class TrackQuickShareDownloadQuery(
    DbWriteQueue dbWriteQueue,
    IClock clock)
{
    public Task<ResultCode> Execute(
        QuickShareContext quickShare,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                quickShare: quickShare),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        QuickShareContext quickShare)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE qsh_quick_shares
                     SET qsh_downloads_count = qsh_downloads_count + 1,
                         qsh_last_accessed_at = $now
                     WHERE qsh_id = $quickShareId
                         AND (qsh_max_downloads IS NULL OR qsh_downloads_count < qsh_max_downloads)
                     RETURNING qsh_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$quickShareId", quickShare.Id)
            .WithParameter("$now", clock.UtcNow)
            .Execute();

        if (result.IsEmpty)
        {
            Log.Information(
                "QuickShare '{ExternalId}' download counter not incremented — limit reached",
                quickShare.ExternalId);
            return ResultCode.LimitReached;
        }

        return ResultCode.Ok;
    }

    public enum ResultCode
    {
        Ok = 0,
        LimitReached
    }
}
