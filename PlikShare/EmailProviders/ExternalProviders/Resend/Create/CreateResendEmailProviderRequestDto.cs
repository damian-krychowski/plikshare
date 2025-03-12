using PlikShare.EmailProviders.Id;

namespace PlikShare.EmailProviders.ExternalProviders.Resend.Create;

public record CreateResendEmailProviderRequestDto(
    string Name,
    string EmailFrom, 
    string ApiKey);

public record CreateResendEmailProviderResponseDto(
    EmailProviderExtId ExternalId);