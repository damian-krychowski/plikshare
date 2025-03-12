using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Integrations.Id;
using Serilog;

namespace PlikShare.Integrations.UpdateName;

public class UpdateIntegrationNameQuery(DbWriteQueue dbWriteQueue)
{
    public Task<Result> Execute(
        IntegrationExtId externalId,
        string name,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                externalId: externalId,
                name: name),
            cancellationToken: cancellationToken);
    }

    private Result ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        IntegrationExtId externalId,
        string name)
    {
        try
        {
            var result = dbWriteContext
                .OneRowCmd(
                    sql: @"
                        UPDATE i_integrations
                        SET i_name = $name
                        WHERE i_external_id = $externalId
                        RETURNING i_id
                    ",
                    readRowFunc: reader => reader.GetInt32(0))
                .WithParameter("$name", name)
                .WithParameter("$externalId", externalId.Value)
                .Execute();

            return result.IsEmpty
                ? new Result(Code: ResultCode.NotFound)
                : new Result(Code: ResultCode.Ok, IntegrationId: result.Value);
        }
        catch (SqliteException e)
        {
            if (e.HasUniqueConstraintFailed(tableName: "i_storages", columnName: "i_name"))
            {
                return new Result(Code: ResultCode.NameNotUnique);
            }
            
            Log.Error(e, "Something went wrong while updating Integration '{IntegrationExternalId}' name to '{Name}'",
                externalId,
                name);
            
            throw;
        }
        catch (Exception e)
        {
            Log.Error(e, "Something went wrong while updating Integration '{IntegrationExternalId}' name to '{Name}'",
                externalId,
                name);
            
            throw;
        }
    }

    public readonly record struct Result(
        ResultCode Code,
        int IntegrationId = default);
    
    public enum ResultCode
    {
        Ok = 0,
        NotFound,
        NameNotUnique
    }
}