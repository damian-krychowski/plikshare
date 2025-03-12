namespace PlikShare.EmailProviders.ExternalProviders.Smtp.Create;

public record CreateSmtpEmailProviderRequestDto(
    string Name,
    string EmailFrom,
    string Hostname,
    int Port,
    SslMode SslMode,
    string Username,
    string Password);