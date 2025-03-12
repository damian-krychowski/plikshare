using PlikShare.BoxLinks.Cache;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.BoxLinks.Delete;

public class DeleteBoxLinkQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        BoxLinkContext boxLink,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                boxLink: boxLink),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        BoxLinkContext boxLink)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     DELETE FROM bl_box_links
                     WHERE bl_id = $boxLinkId
                     RETURNING bl_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$boxLinkId", boxLink.Id)
            .Execute();
        
        if (result.IsEmpty)
        {
            Log.Warning("Could not delete BoxLink '{BoxLinkExternalId}' because BoxLink was not found",
                boxLink.ExternalId);

            return ResultCode.BoxLinkNotFound;
        }

        Log.Information("BoxLink '{BoxLinkExternalId} ({BoxLinkId})' was deleted.",
            boxLink.ExternalId,
            result.Value);

        return ResultCode.Ok;
    }

    public enum ResultCode
    {
        Ok = 0,
        BoxLinkNotFound
    }
}