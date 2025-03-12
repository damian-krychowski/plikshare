using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.EmailProviders.Entities;
using PlikShare.EmailProviders.Id;

namespace PlikShare.EmailProviders.ResendConfirmationEmail;

public class GetEmailProviderQuery(
    IMasterDataEncryption masterDataEncryption,
    PlikShareDb plikShareDb)
{
    public Result Execute(
        EmailProviderExtId externalId)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: """
                     SELECT
                         ep_id,  
                         ep_is_active,
                         ep_type,
                         ep_name,
                         ep_email_from,
                         ep_details_encrypted,
                         ep_confirmation_code,
                         ep_is_confirmed
                     FROM ep_email_providers
                     WHERE ep_external_id = $externalId
                     LIMIT 1
                     """,
                readRowFunc: reader => new EmailProvider(
                    Id: reader.GetInt32(0),
                    ExternalId: externalId,
                    IsActive: reader.GetBoolean(1),
                    Type: EmailProviderType.Build(reader.GetString(2)),
                    Name: reader.GetString(3),
                    EmailFrom: reader.GetString(4),
                    DetailsJson: masterDataEncryption.Decrypt(
                        reader.GetFieldValue<byte[]>(5)),
                    ConfirmationCode: reader.GetString(6),
                    IsConfirmed: reader.GetBoolean(7)))
            .WithParameter("$externalId", externalId.Value)
            .Execute();

        return result.IsEmpty
            ? new Result(
                Code: ResultCode.NotFound)
            : new Result(
                Code: ResultCode.Ok,
                EmailProvider: result.Value);
    }
    
    public enum ResultCode
    {
        Ok,
        NotFound
    }

    public record EmailProvider(
        int Id,
        EmailProviderExtId ExternalId,
        bool IsActive,
        EmailProviderType Type,
        string Name,
        string EmailFrom,
        string DetailsJson,
        string ConfirmationCode,
        bool IsConfirmed);

    public readonly record struct Result(
        ResultCode Code,
        EmailProvider? EmailProvider = null);
}