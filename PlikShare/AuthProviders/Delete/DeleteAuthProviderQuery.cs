using PlikShare.AuthProviders.Id;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.AuthProviders.Delete;

public class DeleteAuthProviderQuery(DbWriteQueue dbWriteQueue)
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
        DbWriteQueue.Context dbWriteContext,
        AuthProviderExtId externalId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var result = dbWriteContext
                .OneRowCmd(
                    sql: """
                         DELETE FROM ap_auth_providers
                         WHERE ap_external_id = $externalId
                         RETURNING ap_id
                         """,
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$externalId", externalId.Value)
                .Execute();

            if (result.IsEmpty)
            {
                transaction.Rollback();
                return new Result(Code: ResultCode.NotFound);
            }

            dbWriteContext
                .Cmd(
                    sql: """
                         DELETE FROM ul_user_logins
                         WHERE ul_login_provider = $loginProvider
                         """,
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$loginProvider", externalId.Value)
                .Execute();

            transaction.Commit();

            Log.Information(
                "Auth Provider '{AuthProviderExternalId}' was deleted.",
                externalId);

            return new Result(
                Code: ResultCode.Ok,
                AuthProviderId: result.Value);
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(
                e,
                "Something went wrong while deleting Auth Provider '{AuthProviderExternalId}'",
                externalId);

            throw;
        }
    }

    public readonly record struct Result(
        ResultCode Code,
        int AuthProviderId = 0);

    public enum ResultCode
    {
        Ok = 0,
        NotFound
    }
}
