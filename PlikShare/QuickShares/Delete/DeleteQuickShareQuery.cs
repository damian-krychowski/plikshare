using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.QuickShares.Cache;
using Serilog;

namespace PlikShare.QuickShares.Delete;

public class DeleteQuickShareQuery(DbWriteQueue dbWriteQueue)
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

    private static ResultCode ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        QuickShareContext quickShare)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     DELETE FROM qs_quick_shares
                     WHERE qs_id = $quickShareId
                     RETURNING qs_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$quickShareId", quickShare.Id)
            .Execute();

        if (result.IsEmpty)
        {
            Log.Warning(
                "Could not delete QuickShare '{ExternalId}' because it was not found",
                quickShare.ExternalId);
            return ResultCode.NotFound;
        }

        Log.Information(
            "QuickShare '{ExternalId} ({Id})' was deleted",
            quickShare.ExternalId,
            result.Value);

        return ResultCode.Ok;
    }

    public enum ResultCode
    {
        Ok = 0,
        NotFound
    }
}
