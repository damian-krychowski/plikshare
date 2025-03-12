using PlikShare.EmailProviders.Id;

namespace PlikShare.EmailProviders.ExternalProviders.AwsSes.Create.Contracts;

public record CreateAwsSesEmailProviderRequestDto(
    string Name,
    string EmailFrom,
    string AccessKey,
    string SecretAccessKey,
    string Region);

public record CreateAwsSesEmailProviderResponseDto(
    EmailProviderExtId ExternalId);