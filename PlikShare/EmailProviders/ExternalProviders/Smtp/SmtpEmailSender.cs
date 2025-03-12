using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;
using PlikShare.Core.Emails;
using Serilog;

namespace PlikShare.EmailProviders.ExternalProviders.Smtp;

public class SmtpEmailSender : IEmailSender
{
    private readonly string _emailFrom;
    private readonly string _hostname;
    private readonly int _port;
    private readonly SecureSocketOptions _secureSocketOptions;
    private readonly string _username;
    private readonly string _password;

    public SmtpEmailSender(
        string emailFrom,
        string hostname,
        int port,
        SslMode sslMode,
        string username,
        string password)
    {
        _emailFrom = emailFrom;
        _hostname = hostname;
        _port = port;
        _secureSocketOptions = GetSecureSocketOptions(sslMode);
        _username = username;
        _password = password;
    }

    public async Task SendEmail(
        string to,
        string subject,
        string htmlContent, 
        CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage(
            from: [MailboxAddress.Parse(_emailFrom)],
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
                host: _hostname,
                port: _port, 
                options: _secureSocketOptions, 
                cancellationToken: cancellationToken);
    
            await client.AuthenticateAsync(
                _username,
                _password,
                cancellationToken);
    
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
                _hostname,
                _port);
            
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