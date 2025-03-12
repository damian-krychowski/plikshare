using PlikShare.Boxes.Cache;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Boxes.UpdateHeader;

public class UpdateBoxHeaderQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        BoxContext box,
        string json,
        string html,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                box: box,
                json: json,
                html: html),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        BoxContext box,
        string json,
        string html)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE bo_boxes
                     SET 
                         bo_header_json = $json,
                         bo_header_html = $html
                     WHERE bo_id = $boxId
                     RETURNING bo_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$json", json)
            .WithParameter("$html", html)
            .WithParameter("$boxId", box.Id)
            .Execute();

        if (result.IsEmpty)
        {
            Log.Warning("Could not update Box '{BoxExternalId}' headerHtml and headerJson because Box was not found.",
                box.ExternalId);

            return ResultCode.BoxNotFound;
        }

        Log.Information("Box '{BoxExternalId}' headerHtml and headerJson  was updated.",
            box.ExternalId);

        return ResultCode.Ok;
    }

    public enum ResultCode
    {
        Ok = 0,
        BoxNotFound
    }
}