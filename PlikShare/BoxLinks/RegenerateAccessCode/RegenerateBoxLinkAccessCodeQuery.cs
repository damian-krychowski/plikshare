using PlikShare.BoxLinks.Cache;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using Serilog;

namespace PlikShare.BoxLinks.RegenerateAccessCode;

public class RegenerateBoxLinkAccessCodeQuery(DbWriteQueue dbWriteQueue)
{
    public Task<Result> Execute(
        BoxLinkContext boxLink,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                boxLink: boxLink),
            cancellationToken: cancellationToken);
    }

    private Result ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        BoxLinkContext boxLink)
    {
        var accessCode = Guid.NewGuid().ToBase62();

        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE bl_box_links
                     SET bl_access_code = $accessCode
                     WHERE bl_id = $boxLinkId
                     RETURNING bl_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$boxLinkId", boxLink.Id)
            .WithParameter("$accessCode", accessCode)
            .Execute();
        
        if (result.IsEmpty)
        {
            Log.Warning("Could not regenerate access-code to '{AccessCode}' of BoxLink '{BoxLinkExternalId}' because BoxLink was not found",
                accessCode,
                boxLink.ExternalId);

            return new Result(
                Code: ResultCode.BoxLinkNotFound);
        }

        Log.Information("BoxLink '{BoxLinkExternalId} ({BoxLinkId})' access-code was regenerated to '{AccessCode}'.",
            boxLink.ExternalId,
            result.Value,
            accessCode);

        return new Result(
            Code: ResultCode.Ok,
            AccessCode: accessCode);
    }

    public readonly record struct Result(
        ResultCode Code,
        string? AccessCode = null);
    
    public enum ResultCode
    {
        Ok = 0,
        BoxLinkNotFound
    }
}