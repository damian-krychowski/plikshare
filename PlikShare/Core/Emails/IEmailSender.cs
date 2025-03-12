namespace PlikShare.Core.Emails;

public interface IEmailSender
{
    Task SendEmail(
        string to,
        string subject,
        string htmlContent, 
        CancellationToken cancellationToken = default);
}