using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.EmailProviders.Id;
using Serilog;

namespace PlikShare.EmailProviders.UpdateName;

public class UpdateEmailProviderNameQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        EmailProviderExtId externalId,
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

    private ResultCode ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        EmailProviderExtId externalId,
        string name)
    {
        try
        {
            var result = dbWriteContext
                .OneRowCmd(
                    sql: """
                         UPDATE ep_email_providers
                         SET ep_name = $name
                         WHERE ep_external_id = $externalId
                         RETURNING ep_id
                         """,
                    readRowFunc: reader => reader.GetInt32(0))
                .WithParameter("$name", name)
                .WithParameter("$externalId", externalId.Value)
                .Execute();

            return result.IsEmpty
                ? ResultCode.NotFound
                : ResultCode.Ok;
        }
        catch (SqliteException e)
        {
            if (e.HasUniqueConstraintFailed(tableName: "ep_email_providers", columnName: "ep_name"))
            {
                return ResultCode.NameNotUnique;
            }
            
            Log.Error(e, "Something went wrong while updating Email Provider '{EmailProviderExternalId}' name to '{Name}'",
                externalId,
                name);
            
            throw;
        }
        catch (Exception e)
        {
            Log.Error(e, "Something went wrong while updating Email Provider '{EmailProviderExternalId}' name to '{Name}'",
                externalId,
                name);
            
            throw;
        }
    }
    
    public enum ResultCode
    {
        Ok = 0,
        NotFound,
        NameNotUnique
    }
}