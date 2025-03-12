using PlikShare.Core.Emails;
using PlikShare.Core.Utils;
using PlikShare.EmailProviders.Entities;
using PlikShare.EmailProviders.ExternalProviders.AwsSes;
using PlikShare.EmailProviders.ExternalProviders.Resend;
using PlikShare.EmailProviders.ExternalProviders.Smtp;

namespace PlikShare.EmailProviders.EmailSender;

public class EmailSenderFactory(
    SmtpEmailSenderFactory smtpEmailSenderFactory,
    ResendEmailSenderFactory resendEmailSenderFactory)
{
    public IEmailSender Build(
        EmailProviderType emailProviderType,
        string emailFrom,
        string detailsJson)
    {
        if (emailProviderType == EmailProviderType.AwsSes)
            return BuildAwsSesEmailSender(
                emailFrom, 
                detailsJson);

        if (emailProviderType == EmailProviderType.Resend)
            return BuildResendEmailSender(
                emailFrom, 
                detailsJson);

        if (emailProviderType == EmailProviderType.Smtp)
            return BuildSmtpEmailSender(
                emailFrom,
                detailsJson);

        throw new InvalidOperationException(
            $"Unknown type '{emailProviderType.Value}' of Email Provider");
    }

    private SmtpEmailSender BuildSmtpEmailSender(
        string emailFrom,
        string detailsJson)
    {
        var details = Json.Deserialize<SmtpDetailsEntity>(
            detailsJson);

        if (details is null)
        {
            throw new InvalidOperationException(
                "Could not deserialize details of Resend Email Provider");
        }

        return smtpEmailSenderFactory.Build(emailFrom, details);
    }
    
    private ResendEmailSender BuildResendEmailSender(
        string emailFrom, 
        string detailsJson)
    {
        var details = Json.Deserialize<ResendDetailsEntity>(
            detailsJson);

        if (details is null)
        {
            throw new InvalidOperationException(
                "Could not deserialize details of Resend Email Provider");
        }

        return resendEmailSenderFactory.Build(emailFrom, details);
    }

    private static AwsSesEmailSender BuildAwsSesEmailSender(
        string emailFrom,
        string detailsJson)
    {
        var details = Json.Deserialize<AwsSesDetailsEntity>(
            detailsJson);

        if (details is null)
        {
            throw new InvalidOperationException(
                "Could not deserialize details of AWS SES Email Provider");
        }

        return AwsSesEmailSenderFactory.Build(
            emailFrom: emailFrom,
            details: details);
    }
}