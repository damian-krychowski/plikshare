using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.EmailProviders.Id;

namespace PlikShare.EmailProviders.Delete;

public class DeleteEmailProviderQuery(DbWriteQueue dbWriteQueue)
{
    public Task<Result> Execute(
        EmailProviderExtId externalId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                externalId: externalId),
            cancellationToken: cancellationToken);
    }

    private Result ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        EmailProviderExtId externalId)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     DELETE FROM ep_email_providers
                     WHERE ep_external_id = $externalId
                     RETURNING ep_id, ep_name, ep_type
                     """,
                readRowFunc: reader => new {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Type = reader.GetString(2)
                })
            .WithParameter("$externalId", externalId.Value)
            .Execute();

        return result.IsEmpty
            ? new Result(
                Code: ResultCode.NotFound)
            : new Result(
                Code: ResultCode.Ok,
                EmailProviderId: result.Value.Id,
                Name: result.Value.Name,
                Type: result.Value.Type);
    }
    
    public readonly record struct Result(
        ResultCode Code,
        int EmailProviderId = 0,
        string? Name = null,
        string? Type = null);
    
    public enum ResultCode
    {
        Ok = 0,
        NotFound
    }
}