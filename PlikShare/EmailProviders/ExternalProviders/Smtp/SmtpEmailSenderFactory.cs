namespace PlikShare.EmailProviders.ExternalProviders.Smtp;

public class SmtpEmailSenderFactory
{
    public SmtpEmailSender Build(string emailFrom, SmtpDetailsEntity details)
    {
        return new SmtpEmailSender(
            emailFrom: emailFrom,
            hostname: details.Hostname,
            port: details.Port,
            sslMode: details.SslMode,
            username: details.Username,
            password: details.Password);
    }
}