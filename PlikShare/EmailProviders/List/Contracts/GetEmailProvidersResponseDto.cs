using PlikShare.EmailProviders.Id;

namespace PlikShare.EmailProviders.List.Contracts;

public record GetEmailProvidersResponseDto(
    GetEmailProvidersItemResponseDto[] Items);

public record GetEmailProvidersItemResponseDto(
    EmailProviderExtId ExternalId,
    string Type,
    string Name,
    string EmailFrom,
    bool IsConfirmed,
    bool IsActive);
