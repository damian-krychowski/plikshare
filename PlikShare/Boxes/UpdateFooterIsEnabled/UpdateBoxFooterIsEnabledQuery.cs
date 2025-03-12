using PlikShare.Boxes.Cache;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Boxes.UpdateFooterIsEnabled;

public class UpdateBoxFooterIsEnabledQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        BoxContext box,
        bool isFooterEnabled,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                box: box,
                isFooterEnabled: isFooterEnabled),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        BoxContext box,
        bool isFooterEnabled)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE bo_boxes
                     SET bo_footer_is_enabled = $isFooterEnabled
                     WHERE bo_id = $boxId
                     RETURNING bo_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$isFooterEnabled", isFooterEnabled)
            .WithParameter("$boxId", box.Id)
            .Execute();

        if (result.IsEmpty)
        {
            Log.Warning("Could not update Box '{BoxExternalId}' isFooterEnabled to '{IsFooterEnabled}' because Box was not found.",
                box.ExternalId,
                isFooterEnabled);

            return ResultCode.BoxNotFound;
        }

        Log.Information("Box '{BoxExternalId}' isFooterEnabled was updated to '{IsFooterEnabled}'.",
            box.ExternalId,
            isFooterEnabled);

        return ResultCode.Ok;
    }

    public enum ResultCode
    {
        Ok = 0,
        BoxNotFound
    }
}