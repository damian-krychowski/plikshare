using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;
using PlikShare.Core.Emails;
using Serilog;

namespace PlikShare.EmailProviders.ExternalProviders.Smtp;

public class SmtpEmailSender(
    string emailFrom,
    string hostname,
    int port,
    SslMode sslMode,
    bool requiresAuthentication,
    string username,
    string password) : IEmailSender
{
    private readonly SecureSocketOptions _secureSocketOptions = GetSecureSocketOptions(sslMode);

    public async Task SendEmail(
        string to,
        string subject,
        string htmlContent, 
        CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage(
            from: [MailboxAddress.Parse(emailFrom)],
            to: [MailboxAddress.Parse(to)],
            subject: subject,
            body: new TextPart(TextFormat.Html)
            {
                Text = htmlContent
            });
        
        try
        {
            using var client = new SmtpClient();
            
            await client.ConnectAsync(
                host: hostname,
                port: port, 
                options: _secureSocketOptions, 
                cancellationToken: cancellationToken);
    
            if (requiresAuthentication)
            {
                await client.AuthenticateAsync(
                    username,
                    password,
                    cancellationToken);
            }

            await client.SendAsync(
                message,
                cancellationToken);
    
            await client.DisconnectAsync(
                true,
                cancellationToken);
        }
        catch (Exception e)
        {
            Log.Error(e, "Cannot send email '{Subject}' to '{Recipient}' with SMTP {Hostname}:{Port}", 
                subject, 
                to,
                hostname,
                port);
            
            throw;
        }
    }

    private static SecureSocketOptions GetSecureSocketOptions(SslMode sslMode)
    {
        return sslMode switch
        {
            SslMode.None => SecureSocketOptions.None,
            SslMode.Auto => SecureSocketOptions.Auto,
            SslMode.SslOnConnect => SecureSocketOptions.StartTls,
            SslMode.StartTls => SecureSocketOptions.StartTls,
            SslMode.StartTlsWhenAvailable => SecureSocketOptions.StartTlsWhenAvailable,
            _ => throw new ArgumentOutOfRangeException(nameof(sslMode), sslMode, null)
        };
    }
}