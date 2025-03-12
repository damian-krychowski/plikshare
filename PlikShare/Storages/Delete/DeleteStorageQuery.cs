using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Storages.Id;
using Serilog;

namespace PlikShare.Storages.Delete;

public class DeleteStorageQuery(DbWriteQueue dbWriteQueue)
{
    public Task<Result> Execute(
        StorageExtId externalId,
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
        StorageExtId externalId)
    {
        try
        {
            var result = dbWriteContext
                .OneRowCmd(
                    sql: """
                         DELETE FROM s_storages
                         WHERE s_external_id = $externalId
                         RETURNING s_id
                         """,
                    readRowFunc: reader => reader.GetInt32(0))
                .WithParameter("$externalId", externalId.Value)
                .Execute();

            if (result.IsEmpty)
            {
                Log.Warning("Could not delete Storage '{StorageExternalId}' because it was not found.",
                    externalId);

                return new Result(
                    Code: ResultCode.NotFound);
            }
            
            Log.Information("Storage '{StorageExternalId}' was deleted",
                externalId);

            return new Result(
                Code: ResultCode.Ok,
                StorageId: result.Value);
        }
        catch (SqliteException e) when(e.HasForeignKeyFailed())
        {
            return new Result(
                Code: ResultCode.WorkspacesOrIntegrationAttached);
        }
        catch (Exception e)
        {
            Log.Error(e, "Something went wrong while deleting Storage '{StorageExternalId}'",
                externalId);

            throw;
        }
    }

    public readonly record struct Result(
        ResultCode Code,
        int StorageId = 0);
    
    public enum ResultCode
    {
        Ok = 0,
        NotFound,
        WorkspacesOrIntegrationAttached
    }
}