using PlikShare.Boxes.Cache;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Boxes.UpdateIsEnabled;

public class UpdateBoxIsEnabledQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        BoxContext box,
        bool isEnabled,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                box: box,
                isEnabled: isEnabled),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        BoxContext box,
        bool isEnabled)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE bo_boxes
                     SET bo_is_enabled = $isEnabled
                     WHERE bo_id = $boxId
                     RETURNING bo_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$isEnabled", isEnabled)
            .WithParameter("$boxId", box.Id)
            .Execute();

        if (result.IsEmpty)
        {
            Log.Warning("Could not update Box '{BoxExternalId}' isEnabled to '{IsEnabled}' because Box was not found.",
                box.ExternalId,
                isEnabled);

            return ResultCode.BoxNotFound;
        }

        Log.Information("Box '{BoxExternalId}' isEnabled was updated to '{IsEnabled}'.",
            box.ExternalId,
            isEnabled);

        return ResultCode.Ok;
    }

    public enum ResultCode
    {
        Ok = 0,
        BoxNotFound
    }
}