using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.EmailProviders.Id;

namespace PlikShare.EmailProviders.Confirm;

public class ConfirmEmailProviderQuery(DbWriteQueue dbWriteQueue)
{
    public Task<Result> Execute(
        EmailProviderExtId externalId,
        string confirmationCode,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                externalId: externalId,
                confirmationCode: confirmationCode),
            cancellationToken: cancellationToken);
    }

    private Result ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        EmailProviderExtId externalId,
        string confirmationCode)
    {
        var emailProvider = dbWriteContext
            .OneRowCmd(
                sql: """
                     SELECT 
                         ep_id,
                         ep_confirmation_code,
                         ep_is_confirmed
                     FROM ep_email_providers
                     WHERE ep_external_id = $externalId
                     LIMIT 1
                     """,
                readRowFunc: reader => new
                {
                    Id = reader.GetInt32(0),
                    ConfirmationCode = reader.GetString(1),
                    IsConfirmed = reader.GetBoolean(2)
                })
            .WithParameter("$externalId", externalId.Value)
            .Execute();

        if (emailProvider.IsEmpty)
            return new Result(Code: ResultCode.NotFound);

        if (emailProvider.Value.IsConfirmed)
            return new Result(Code: ResultCode.AlreadyConfirmed);

        if (emailProvider.Value.ConfirmationCode != confirmationCode)
            return new Result(Code: ResultCode.WrongConfirmationCode);

        var confirmedProvider = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE ep_email_providers
                     SET ep_is_confirmed = TRUE
                     WHERE ep_id = $id
                     RETURNING ep_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$id", emailProvider.Value.Id)
            .Execute();
        
        if(confirmedProvider.IsEmpty)
            return new Result(Code: ResultCode.NotFound);

        return new Result(
            Code: ResultCode.Ok,
            EmailProviderId: confirmedProvider.Value);
    }

    public enum ResultCode
    {
        Ok,
        NotFound,
        AlreadyConfirmed,
        WrongConfirmationCode
    }

    public readonly record struct Result(
        ResultCode Code,
        int EmailProviderId = 0);
}