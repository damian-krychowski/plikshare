namespace PlikShare.EmailProviders.ExternalProviders.Resend;

public record ResendRequestBody(
    string From,
    string[] To,
    string Subject,
    string Html);