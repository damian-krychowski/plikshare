using PlikShare.Boxes.Cache;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Boxes.UpdateHeaderIsEnabled;

public class UpdateBoxHeaderIsEnabledQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        BoxContext box,
        bool isHeaderEnabled,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                box: box,
                isHeaderEnabled: isHeaderEnabled),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        BoxContext box,
        bool isHeaderEnabled)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE bo_boxes
                     SET bo_header_is_enabled = $isHeaderEnabled
                     WHERE bo_id = $boxId                         
                     RETURNING bo_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$isHeaderEnabled", isHeaderEnabled)
            .WithParameter("$boxId", box.Id)
            .Execute();

        if (result.IsEmpty)
        {
            Log.Warning("Could not update Box '{BoxExternalId}' isHeaderEnabled to '{IsHeaderEnabled}' because Box was not found.",
                box.ExternalId,
                isHeaderEnabled);

            return ResultCode.BoxNotFound;
        }

        Log.Information("Box '{BoxExternalId}' isHeaderEnabled was updated to '{IsHeaderEnabled}'.",
            box.ExternalId,
            isHeaderEnabled);

        return ResultCode.Ok;
    }

    public enum ResultCode
    {
        Ok = 0,
        BoxNotFound
    }
}