using PlikShare.BoxLinks.Cache;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.BoxLinks.UpdateIsEnabled;

public class UpdateBoxLinkIsEnabledQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        BoxLinkContext boxLink,
        bool isEnabled,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                boxLink: boxLink,
                isEnabled: isEnabled),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        BoxLinkContext boxLink,
        bool isEnabled)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE bl_box_links
                     SET bl_is_enabled = $isEnabled
                     WHERE bl_id = $boxLinkId
                     RETURNING bl_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$isEnabled", isEnabled)
            .WithParameter("$boxLinkId", boxLink.Id)
            .Execute();
            
        if (result.IsEmpty)
        {
            Log.Warning("Could not update BoxLink '{BoxLinkExternalId}' is-enabled to '{IsEnabled}' because BoxLink was not found",
                boxLink.ExternalId,
                isEnabled);

            return ResultCode.BoxLinkNotFound;
        }

        Log.Information("BoxLink '{BoxLinkExternalId} ({BoxLinkId})' is-enabled was updated to '{IsEnabled}'",
            boxLink.ExternalId,
            result.Value,
            isEnabled);

        return ResultCode.Ok;
    }
    
    public enum ResultCode
    {
        Ok = 0,
        BoxLinkNotFound
    }
}