using PlikShare.Core.Emails;
using PlikShare.Core.Emails.Templates;
using PlikShare.GeneralSettings;
using PlikShare.Users.Entities;

namespace PlikShare.EmailProviders.SendConfirmationEmail;

public class EmailProviderConfirmationEmail(
    AppSettings appSettings,
    GenericEmailTemplate genericEmailTemplate)
{
    public async Task Send(
        string emailProviderName,
        string confirmationCode,
        Email to, 
        IEmailSender emailSender,
        CancellationToken cancellationToken = default)
    {
        var (title, content) = Emails.EmailProviderConfirmation(
            applicationName: appSettings.ApplicationName.Name,
            emailProviderName: emailProviderName,
            confirmationCode: confirmationCode);
        
        await emailSender.SendEmail(
            to: to.Value,
            subject: title,
            htmlContent: genericEmailTemplate.Build(
                title: title,
                content: content),
            cancellationToken: cancellationToken);
    }
}