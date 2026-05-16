using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.QuickShares.Cache;
using Serilog;

namespace PlikShare.QuickShares.UpdateMaxDownloads;

public class UpdateQuickShareMaxDownloadsQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        QuickShareContext quickShare,
        int? maxDownloads,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                quickShare: quickShare,
                maxDownloads: maxDownloads),
            cancellationToken: cancellationToken);
    }

    private static ResultCode ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        QuickShareContext quickShare,
        int? maxDownloads)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE qs_quick_shares
                     SET qs_max_downloads = $maxDownloads
                     WHERE qs_id = $quickShareId
                     RETURNING qs_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$quickShareId", quickShare.Id)
            .WithParameter("$maxDownloads", maxDownloads)
            .Execute();

        if (result.IsEmpty)
        {
            Log.Warning(
                "Could not update QuickShare '{ExternalId}' max-downloads to '{MaxDownloads}' because it was not found",
                quickShare.ExternalId,
                maxDownloads);
            return ResultCode.NotFound;
        }

        Log.Information(
            "QuickShare '{ExternalId} ({Id})' max-downloads updated to '{MaxDownloads}'",
            quickShare.ExternalId,
            result.Value,
            maxDownloads);

        return ResultCode.Ok;
    }

    public enum ResultCode
    {
        Ok = 0,
        NotFound
    }
}
