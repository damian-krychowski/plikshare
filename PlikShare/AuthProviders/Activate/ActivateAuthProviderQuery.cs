using PlikShare.AuthProviders.Id;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.AuthProviders.Activate;

public class ActivateAuthProviderQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        AuthProviderExtId externalId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                externalId: externalId),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        AuthProviderExtId externalId)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE ap_auth_providers
                     SET ap_is_active = TRUE
                     WHERE ap_external_id = $externalId
                     RETURNING ap_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$externalId", externalId.Value)
            .Execute();

        if (result.IsEmpty)
        {
            return ResultCode.NotFound;
        }

        Log.Information(
            "Auth Provider '{AuthProviderExternalId}' was activated.",
            externalId);

        return ResultCode.Ok;
    }

    public enum ResultCode
    {
        Ok,
        NotFound
    }
}
