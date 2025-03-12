using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.EmailProviders.Id;
using Serilog;

namespace PlikShare.EmailProviders.Deactivate;

public class DeactivateEmailProviderQuery(DbWriteQueue dbWriteQueue)
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
        DbWriteQueue.Context dbWriteContext,
        EmailProviderExtId externalId)
    {
        var deactivated = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE ep_email_providers
                     SET ep_is_active = FALSE
                     WHERE ep_external_id = $externalId
                     RETURNING ep_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$externalId", externalId.Value)
            .Execute();
        
        if (deactivated.IsEmpty)
        {
            return new Result(Code: ResultCode.NotFound);
        }
        
        Log.Information("Email Provider '{EmailProviderExternalId} was deactivated",
            externalId);

        return new Result(
            Code: ResultCode.Ok,
            EmailProviderId: deactivated.Value);
    }
    
    public enum ResultCode
    {
        Ok,
        NotFound
    }

    public readonly record struct Result(
        ResultCode Code,
        int EmailProviderId = default);
}