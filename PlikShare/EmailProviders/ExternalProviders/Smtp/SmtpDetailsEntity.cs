namespace PlikShare.EmailProviders.ExternalProviders.Smtp;

public record SmtpDetailsEntity(
    string Hostname,
    int Port,
    SslMode SslMode,
    string Username,
    string Password);

public enum SslMode
{
    None = 0,
    Auto,
    SslOnConnect,
    StartTls,
    StartTlsWhenAvailable
}