using PlikShare.AuthProviders.Id;

namespace PlikShare.AuthProviders.List.Contracts;

public class GetAuthSettingsResponseDto
{
    public required GetAuthProvidersItemDto[] Items { get; init; }
    public required bool IsPasswordLoginEnabled { get; init; }
    public required bool CurrentUserHasSsoLinked { get; init; }
}

public class GetAuthProvidersItemDto
{
    public required AuthProviderExtId ExternalId { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required bool IsActive { get; init; }
    public required string ClientId { get; init; }
    public required string IssuerUrl { get; init; }
}
