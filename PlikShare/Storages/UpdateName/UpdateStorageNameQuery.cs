using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Storages.Id;
using Serilog;

namespace PlikShare.Storages.UpdateName;

public class UpdateStorageNameQuery(DbWriteQueue dbWriteQueue)
{
    public Task<Result> Execute(
        StorageExtId externalId,
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
        StorageExtId externalId,
        string name)
    {
        try
        {
            var result = dbWriteContext
                .OneRowCmd(
                    sql: """
                         UPDATE s_storages
                         SET s_name = $name
                         WHERE s_external_id = $externalId
                         RETURNING s_id
                         """,
                    readRowFunc: reader => reader.GetInt32(0))
                .WithParameter("$name", name)
                .WithParameter("$externalId", externalId.Value)
                .Execute();

            return result.IsEmpty
                ? new Result(Code: ResultCode.NotFound)
                : new Result(Code: ResultCode.Ok, StorageId: result.Value);
        }
        catch (SqliteException e)
        {
            if (e.HasUniqueConstraintFailed(tableName: "s_storages", columnName: "s_name"))
            {
                return new Result(Code: ResultCode.NameNotUnique);
            }
            
            Log.Error(e, "Something went wrong while updating Storage '{StorageExternalId}' name to '{Name}'",
                externalId,
                name);
            
            throw;
        }
        catch (Exception e)
        {
            Log.Error(e, "Something went wrong while updating Storage '{StorageExternalId}' name to '{Name}'",
                externalId,
                name);
            
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
        NameNotUnique
    }
}