using PlikShare.AuthProviders.Id;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.AuthProviders.Activate;

public class ActivateAuthProviderQuery(DbWriteQueue dbWriteQueue)
{
    public Task<Result> Execute(
        AuthProviderExtId externalId,
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
        AuthProviderExtId externalId)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE ap_auth_providers
                     SET ap_is_active = TRUE
                     WHERE ap_external_id = $externalId
                     RETURNING ap_id, ap_name, ap_type
                     """,
                readRowFunc: reader => new
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Type = reader.GetString(2)
                })
            .WithParameter("$externalId", externalId.Value)
            .Execute();

        if (result.IsEmpty)
        {
            return new Result(Code: ResultCode.NotFound);
        }

        Log.Information(
            "Auth Provider '{AuthProviderExternalId}' was activated.",
            externalId);

        return new Result(
            Code: ResultCode.Ok,
            Name: result.Value.Name,
            Type: result.Value.Type);
    }

    public readonly record struct Result(
        ResultCode Code,
        string? Name = null,
        string? Type = null);

    public enum ResultCode
    {
        Ok,
        NotFound
    }
}
