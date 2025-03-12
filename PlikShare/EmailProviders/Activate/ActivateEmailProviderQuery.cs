using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.EmailProviders.Entities;
using PlikShare.EmailProviders.Id;
using Serilog;

namespace PlikShare.EmailProviders.Activate;

public class ActivateEmailProviderQuery(
    IMasterDataEncryption masterDataEncryption,
    DbWriteQueue dbWriteQueue)
{
    public async Task<Result> Execute(
        EmailProviderExtId externalId,
        CancellationToken cancellationToken)
    {
        var resultEncrypted = await dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                externalId: externalId),
            cancellationToken: cancellationToken);

        return new Result(
            Code: resultEncrypted.Code,
            EmailProvider: resultEncrypted.EmailProvider is null
                ? null
                : new EmailProvider
                {
                    Id = resultEncrypted.EmailProvider.Id,
                    EmailFrom = resultEncrypted.EmailProvider.EmailFrom,
                    Type = resultEncrypted.EmailProvider.Type,
                    DetailsJson = masterDataEncryption.Decrypt(
                        resultEncrypted.EmailProvider.DetailsJsonEncrypted)
                });
    }

    private ResultEncrypted ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        EmailProviderExtId externalId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var activatedProvider = dbWriteContext
                .OneRowCmd(
                    sql: """
                         UPDATE ep_email_providers
                         SET ep_is_active = TRUE
                         WHERE ep_external_id = $externalId
                         RETURNING
                             ep_id,
                             ep_type,
                             ep_email_from,
                             ep_details_encrypted,
                             ep_is_confirmed
                         """,
                    readRowFunc: reader => new 
                    {
                        Id = reader.GetInt32(0),
                        Type = EmailProviderType.Build(reader.GetString(1)),
                        EmailFrom = reader.GetString(2),
                        DetailsJsonEncrypted = reader.GetFieldValue<byte[]>(3),
                        IsConfirmed = reader.GetBoolean(4)
                    },
                    transaction: transaction)
                .WithParameter("$externalId", externalId.Value)
                .Execute();

            if (activatedProvider.IsEmpty)
            {
                transaction.Rollback();
                return new ResultEncrypted(Code: ResultCode.NotFound);
            }

            if (!activatedProvider.Value.IsConfirmed)
            {
                transaction.Rollback();
                return new ResultEncrypted(Code: ResultCode.ProviderNotConfirmed);
            }
            
            var oldActiveProviders = dbWriteContext
                .Cmd(
                    sql: """
                         UPDATE ep_email_providers
                         SET ep_is_active = FALSE
                         WHERE 
                             ep_is_active = TRUE
                             AND ep_id != $id
                         RETURNING 
                             ep_id
                         """,
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$id", activatedProvider.Value.Id)
                .Execute();
            
            transaction.Commit();

            Log.Information(
                "Email Provider '{EmailProviderExternalId} was activated. Following Email Providers were deactivated: {DeactivatedEmailProviderIds}'",
                externalId,
                oldActiveProviders);

            var provider = activatedProvider.Value;

            return new ResultEncrypted(
                Code: ResultCode.Ok,
                EmailProvider: new EmailProviderEncrypted
                {
                    Id = provider.Id,
                    Type = provider.Type,
                    DetailsJsonEncrypted = provider.DetailsJsonEncrypted,
                    EmailFrom = provider.EmailFrom
                });
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while activating Email Provider '{EmailProviderExternalId}'",
                externalId);
            
            throw;
        }
    }
    
    public enum ResultCode
    {
        Ok,
        NotFound,
        ProviderNotConfirmed
    }

    public readonly record struct Result(
        ResultCode Code,
        EmailProvider? EmailProvider = null);

    private readonly record struct ResultEncrypted(
        ResultCode Code,
        EmailProviderEncrypted? EmailProvider = null);

    private class EmailProviderEncrypted
    {
        public required int Id { get; init; }
        public required EmailProviderType Type { get; init; }
        public required byte[] DetailsJsonEncrypted { get; init; }
        public required string EmailFrom { get; init; }
    }

    public class EmailProvider
    {
        public required int Id { get; init; }
        public required EmailProviderType Type { get; init; }
        public required string DetailsJson { get; init; }
        public required string EmailFrom { get; init; }
    }
}