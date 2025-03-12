using PlikShare.BoxLinks.Cache;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.BoxLinks.UpdateName;

public class UpdateBoxLinkNameQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        BoxLinkContext boxLink,
        string name,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                boxLink: boxLink,
                name: name),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        BoxLinkContext boxLink,
        string name)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE bl_box_links
                     SET bl_name = $name
                     WHERE bl_id = $boxLinkId
                     RETURNING bl_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$boxLinkId", boxLink.Id)
            .WithParameter("$name", name)
            .Execute();
        
        if (result.IsEmpty)
        {
            Log.Warning("Could not update BoxLink '{BoxLinkExternalId}' name to '{Name}' because BoxLink was not found",
                boxLink.ExternalId,
                name);

            return ResultCode.BoxLinkNotFound;
        }

        Log.Information("BoxLink '{BoxLinkExternalId} ({BoxLinkId})' name was updated to '{Name}'",
            boxLink.ExternalId,
            result.Value,
            name);

        return ResultCode.Ok;
    }
    
    public enum ResultCode
    {
        Ok = 0,
        BoxLinkNotFound
    }
}