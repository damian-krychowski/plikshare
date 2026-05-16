using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.QuickShares.Cache;
using Serilog;

namespace PlikShare.QuickShares.UpdateMode;

public class UpdateQuickShareModeQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        QuickShareContext quickShare,
        QuickShareMode mode,
        bool allowIndividualFileDownload,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                quickShare: quickShare,
                mode: mode,
                allowIndividualFileDownload: allowIndividualFileDownload),
            cancellationToken: cancellationToken);
    }

    private static ResultCode ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        QuickShareContext quickShare,
        QuickShareMode mode,
        bool allowIndividualFileDownload)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE qs_quick_shares
                     SET qs_mode = $mode,
                         qs_allow_individual_file_download = $allowIndividualFileDownload
                     WHERE qs_id = $quickShareId
                     RETURNING qs_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$quickShareId", quickShare.Id)
            .WithEnumParameter("$mode", mode)
            .WithParameter("$allowIndividualFileDownload", allowIndividualFileDownload)
            .Execute();

        if (result.IsEmpty)
        {
            Log.Warning(
                "Could not update QuickShare '{ExternalId}' mode to '{Mode}' because it was not found",
                quickShare.ExternalId,
                mode);
            return ResultCode.NotFound;
        }

        Log.Information(
            "QuickShare '{ExternalId} ({Id})' mode updated to '{Mode}' (allowIndividual={AllowIndividual})",
            quickShare.ExternalId,
            result.Value,
            mode,
            allowIndividualFileDownload);

        return ResultCode.Ok;
    }

    public enum ResultCode
    {
        Ok = 0,
        NotFound
    }
}
