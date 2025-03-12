using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Integrations.Id;
using Serilog;

namespace PlikShare.Integrations.Deactivate;

public class DeactivateIntegrationQuery(DbWriteQueue dbWriteQueue)
{
    public Task<Result> Execute(
        IntegrationExtId externalId,
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
        IntegrationExtId externalId)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: @"
                    UPDATE i_integrations
                    SET i_is_active = FALSE
                    WHERE i_external_id = $externalId
                    RETURNING
                        i_id,
                        i_type
                ",
                readRowFunc: reader => new Integration(
                    Id: reader.GetInt32(0),
                    Type: reader.GetEnum<IntegrationType>(1)))
            .WithParameter("$externalId", externalId.Value)
            .Execute();

        if (result.IsEmpty)
        {
            Log.Warning("Could not deactivate Integration '{IntegrationExternalId}' because it was not found.",
                externalId);

            return new Result(
                Code: ResultCode.NotFound);
        }

        Log.Information("Integration {IntegrationType} '{IntegrationExternalId}' was deactivated",
            result.Value.Type,
            externalId);

        return new Result(
            Code: ResultCode.Ok,
            Integration: result.Value);
    }

    public readonly record struct Result(
        ResultCode Code,
        Integration Integration = default);

    public readonly record struct Integration(
        int Id,
        IntegrationType Type);

    public enum ResultCode
    {
        Ok = 0,
        NotFound
    }
}