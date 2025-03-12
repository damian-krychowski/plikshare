using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.EmailProviders.EmailSender;
using PlikShare.EmailProviders.Entities;
using Serilog;

namespace PlikShare.Core.Emails;

public static class EmailProviderStartupExtensions
{
    public static void InitializeEmailProvider(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var plikshareDb = app
            .Services
            .GetRequiredService<PlikShareDb>();

        var dateEncryption = app
            .Services
            .GetRequiredService<IMasterDataEncryption>();

        using var connection = plikshareDb.OpenConnection();

        var emailProvider = connection
            .OneRowCmd(
                sql: @"
                    SELECT ep_id, ep_type, ep_email_from, ep_details_encrypted
                    FROM ep_email_providers
                    WHERE ep_is_active = TRUE
                    LIMIT 1
                ",
                readRowFunc: reader => new
                {
                    Id = reader.GetInt32(0),
                    Type = EmailProviderType.Build(reader.GetString(1)),
                    EmailFrom = reader.GetString(2),
                    DetailsJson = dateEncryption.Decrypt(
                        reader.GetFieldValue<byte[]>(3))
                })
            .Execute();

        if (emailProvider.IsEmpty)
        {
            Log.Information("[INITIALIZATION] Email Provider initialization finished. No Email Provider was available.");
            
            return;
        }
        
        var emailProviderStore = app
            .Services
            .GetRequiredService<EmailProviderStore>();

        var emailSenderFactory = app
            .Services
            .GetRequiredService<EmailSenderFactory>();

        emailProviderStore.SetEmailSender(
            emailProviderId: emailProvider.Value.Id,
            emailSender: emailSenderFactory.Build(
                emailProviderType: emailProvider.Value.Type,
                emailFrom: emailProvider.Value.EmailFrom,
                detailsJson: emailProvider.Value.DetailsJson));
        
        Log.Information("[INITIALIZATION] Email Provider '{EmailProvider}' initialization finished.",
            emailProvider.Value.EmailFrom);
    }
}