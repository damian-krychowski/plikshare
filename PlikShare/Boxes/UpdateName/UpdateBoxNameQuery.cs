using PlikShare.Boxes.Cache;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Boxes.UpdateName;

public class UpdateBoxNameQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        BoxContext box,
        string name,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                box: box,
                name: name),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        BoxContext box,
        string name)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE bo_boxes
                     SET bo_name = $name
                     WHERE bo_id = $boxId
                     RETURNING bo_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$name", name)
            .WithParameter("$boxId", box.Id)
            .Execute();

        if (result.IsEmpty)
        {
            Log.Warning("Could not update Box '{BoxExternalId}' name to '{Name}' because Box was not found.",
                box.ExternalId,
                name);

            return ResultCode.BoxNotFound;
        }

        Log.Information("Box '{BoxExternalId}' name was updated to '{Name}'.",
            box.ExternalId,
            name);

        return ResultCode.Ok;
    }

    public enum ResultCode
    {
        Ok = 0,
        BoxNotFound
    }
}