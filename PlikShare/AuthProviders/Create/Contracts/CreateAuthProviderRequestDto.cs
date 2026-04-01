namespace PlikShare.AuthProviders.Create.Contracts;

public class CreateOidcAuthProviderRequestDto
{
    public required string Name { get; init; }
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public required string IssuerUrl { get; init; }
}

public class CreateOidcAuthProviderResponseDto
{
    public required string ExternalId { get; init; }
}
