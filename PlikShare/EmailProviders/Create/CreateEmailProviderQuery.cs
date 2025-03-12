using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.EmailProviders.Entities;
using PlikShare.EmailProviders.Id;
using Serilog;

namespace PlikShare.EmailProviders.Create;

public class CreateEmailProviderQuery(
    DbWriteQueue dbWriteQueue,
    MasterDataEncryptionBufferedFactory masterDataEncryptionBufferedFactory,
    IOneTimeCode oneTimeCode)
{
    public async Task<Result> Execute(
        string name,
        EmailProviderType type,
        string emailFrom,
        string detailsJson,
        CancellationToken cancellationToken)
    {
        var derivedEncryption = await masterDataEncryptionBufferedFactory.Take(
            cancellationToken: cancellationToken);

        return await dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                name: name,
                type: type,
                emailFrom: emailFrom,
                detailsJson: detailsJson,
                derivedEncryption: derivedEncryption),
            cancellationToken: cancellationToken);
    }

    private Result ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        string name,
        EmailProviderType type,
        string emailFrom,
        string detailsJson,
        IDerivedMasterDataEncryption derivedEncryption)
    {
        try
        {
            var emailProviderExternalId = EmailProviderExtId.NewId();
            var confirmationCode = oneTimeCode.Generate();

            var emailProviderId = dbWriteContext
                .OneRowCmd(
                    sql: """
                         INSERT INTO ep_email_providers(
                             ep_external_id, 
                             ep_is_active, 
                             ep_type, 
                             ep_name, 
                             ep_email_from, 
                             ep_details_encrypted, 
                             ep_confirmation_code, 
                             ep_is_confirmed
                         ) VALUES (
                             $externalId,
                             FALSE,
                             $type,
                             $name,
                             $emailFrom,
                             $details,
                             $confirmationCode,
                             FALSE
                         ) 
                         RETURNING ep_id
                         """,
                    readRowFunc: reader => reader.GetInt32(0))
                .WithParameter("$externalId", emailProviderExternalId.Value)
                .WithParameter("$type", type.Value)
                .WithParameter("$name", name)
                .WithParameter("$emailFrom", emailFrom)
                .WithParameter("$details", derivedEncryption.Encrypt(detailsJson))
                .WithParameter("$confirmationCode", confirmationCode)
                .ExecuteOrThrow();
            
            Log.Information("{EmailProviderType} EmailProvider#{EmailProviderId} '{EmailProviderName}' with ExternalId '{EmailProviderExternalId}' was created.",
                type,
                emailProviderId,
                name,
                emailProviderExternalId);

            return new Result(
                Code: ResultCode.Ok,
                EmailProvider: new EmailProvider(
                    Id: emailProviderId,
                    ExternalId: emailProviderExternalId,
                    ConfirmationCode: confirmationCode));
        }
        catch (SqliteException e)
        {
            if (e.HasUniqueConstraintFailed(tableName: "ep_email_providers", columnName: "ep_name"))
            {
                return new Result(Code: ResultCode.NameNotUnique);
            }
            
            Log.Error(e, "Something went wrong while creating {EmailProviderType} email provider '{EmailProviderName}'",
                type,
                name);
            
            throw;
        }
        catch (Exception e)
        {
            Log.Error(e, "Something went wrong while creating {EmailProviderType} email provider '{EmailProviderName}'",
                type,
                name);
            
            throw;
        }
    }
    
    public enum ResultCode
    {
        Ok,
        NameNotUnique
    }
    
    public readonly record struct Result(
        ResultCode Code,
        EmailProvider? EmailProvider = null);

    public record EmailProvider(
        int Id,
        EmailProviderExtId ExternalId,
        string ConfirmationCode);
}