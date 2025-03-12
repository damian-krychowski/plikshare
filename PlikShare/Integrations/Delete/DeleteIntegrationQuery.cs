using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Integrations.Aws.Textract.Jobs.Delete;
using PlikShare.Integrations.Id;
using Serilog;
using Serilog.Events;

namespace PlikShare.Integrations.Delete;

public class DeleteIntegrationQuery(
    DbWriteQueue dbWriteQueue, 
    DeleteTextractJobsSubQuery deleteTextractJobsSubQuery)
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
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            dbWriteContext
                .Connection
                .NonQueryCmd(
                    sql: "PRAGMA defer_foreign_keys = ON;",
                    transaction: transaction)
                .Execute();

            var result = dbWriteContext
                .OneRowCmd(
                    sql: @"
                        DELETE FROM i_integrations
                        WHERE i_external_id = $externalId
                        RETURNING 
                            i_id,
                            i_type
                    ",
                    readRowFunc: reader => new Integration(
                        Id: reader.GetInt32(0),
                        Type: reader.GetEnum<IntegrationType>(1)),
                    transaction: transaction)
                .WithParameter("$externalId", externalId.Value)
                .Execute();

            if (result.IsEmpty)
            {
                transaction.Rollback();

                Log.Warning("Could not delete Integration '{IntegrationExternalId}' because it was not found.",
                    externalId);

                return new Result(
                    Code: ResultCode.NotFound);
            }

            if (result.Value.Type == IntegrationType.AwsTextract)
            {
                var deletedTextractJobs = deleteTextractJobsSubQuery.Execute(
                    integrationId: result.Value.Id,
                    dbWriteContext: dbWriteContext,
                    transaction: transaction);

                transaction.Commit();

                if (Log.IsEnabled(LogEventLevel.Information))
                {
                    var textractJobIds = IdsRange.GroupConsecutiveIds(
                        ids: deletedTextractJobs.Select(x => x.Id));

                    Log.Information("Integration {IntegrationType} '{IntegrationExternalId}' was deleted. " +
                                    "Deleted textract jobs ({TextractJobsCount}): [{TextractJobsIds}]",
                        result.Value.Type,
                        deletedTextractJobs.Count,
                        textractJobIds,
                        externalId);
                }
            }
            else
            {
                transaction.Commit();

                Log.Information("Integration {IntegrationType} '{IntegrationExternalId}' was deleted",
                    result.Value.Type,
                    externalId);
            }

            return new Result(
                Code: ResultCode.Ok,
                Integration: result.Value);
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while deleting Integration '{IntegrationExternalId}'",
                externalId);

            throw;
        }
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