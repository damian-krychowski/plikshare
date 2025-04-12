using PlikShare.BoxLinks.Cache;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.BoxLinks.UpdateWidgetOrigins;

public class UpdateBoxLinkWidgetOriginsQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        BoxLinkContext boxLink,
        List<string> widgetOrigins,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                boxLink: boxLink,
                widgetOrigins: widgetOrigins),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        BoxLinkContext boxLink,
        List<string> widgetOrigins)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE bl_box_links
                     SET bl_widget_origins = $widgetOrigins
                     WHERE bl_id = $boxLinkId
                     RETURNING bl_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$boxLinkId", boxLink.Id)
            .WithJsonParameter("$widgetOrigins", widgetOrigins.Count == 0 
                ? null 
                : widgetOrigins)
            .Execute();
        
        if (result.IsEmpty)
        {
            Log.Warning("Could not update BoxLink '{BoxLinkExternalId}' widget origins to '{WidgetOrigins}' because BoxLink was not found",
                boxLink.ExternalId,
                widgetOrigins);

            return ResultCode.BoxLinkNotFound;
        }

        Log.Information("BoxLink '{BoxLinkExternalId} ({BoxLinkId})' widget origins was updated to '{WidgetOrigins}'",
            boxLink.ExternalId,
            result.Value,
            widgetOrigins);

        return ResultCode.Ok;
    }
    
    public enum ResultCode
    {
        Ok = 0,
        BoxLinkNotFound
    }
}