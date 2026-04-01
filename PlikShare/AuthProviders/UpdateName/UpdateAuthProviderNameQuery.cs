using Microsoft.Data.Sqlite;
using PlikShare.AuthProviders.Id;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.AuthProviders.UpdateName;

public class UpdateAuthProviderNameQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        AuthProviderExtId externalId,
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
        AuthProviderExtId externalId,
        string name)
    {
        try
        {
            var result = dbWriteContext
                .OneRowCmd(
                    sql: """
                         UPDATE ap_auth_providers
                         SET ap_name = $name
                         WHERE ap_external_id = $externalId
                         RETURNING ap_id
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
            if (e.HasUniqueConstraintFailed(tableName: "ap_auth_providers", columnName: "ap_name"))
            {
                return ResultCode.NameNotUnique;
            }

            Log.Error(
                e,
                "Something went wrong while updating Auth Provider '{AuthProviderExternalId}' name to '{Name}'",
                externalId,
                name);

            throw;
        }
        catch (Exception e)
        {
            Log.Error(
                e,
                "Something went wrong while updating Auth Provider '{AuthProviderExternalId}' name to '{Name}'",
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
