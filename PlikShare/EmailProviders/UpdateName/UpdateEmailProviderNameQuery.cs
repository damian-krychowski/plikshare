using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.EmailProviders.Id;
using Serilog;

namespace PlikShare.EmailProviders.UpdateName;

public class UpdateEmailProviderNameQuery(DbWriteQueue dbWriteQueue)
{
    public Task<Result> Execute(
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

    private Result ExecuteOperation(
        SqliteWriteContext dbWriteContext,
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
                         RETURNING ep_id, ep_type
                         """,
                    readRowFunc: reader => new
                    {
                        Id = reader.GetInt32(0),
                        Type = reader.GetString(1)
                    })
                .WithParameter("$name", name)
                .WithParameter("$externalId", externalId.Value)
                .Execute();

            return result.IsEmpty
                ? new Result(Code: ResultCode.NotFound)
                : new Result(Code: ResultCode.Ok, Type: result.Value.Type);
        }
        catch (SqliteException e)
        {
            if (e.HasUniqueConstraintFailed(tableName: "ep_email_providers", columnName: "ep_name"))
            {
                return new Result(Code: ResultCode.NameNotUnique);
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

    public readonly record struct Result(
        ResultCode Code,
        string? Type = null);

    public enum ResultCode
    {
        Ok = 0,
        NotFound,
        NameNotUnique
    }
}