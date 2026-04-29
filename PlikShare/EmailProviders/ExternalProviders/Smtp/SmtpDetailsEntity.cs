namespace PlikShare.EmailProviders.ExternalProviders.Smtp;

public record SmtpDetailsEntity(
    string Hostname,
    int Port,
    SslMode SslMode,
    string Username,
    string Password,
    bool RequiresAuthentication = true);

public enum SslMode
{
    None = 0,
    Auto,
    SslOnConnect,
    StartTls,
    StartTlsWhenAvailable
}