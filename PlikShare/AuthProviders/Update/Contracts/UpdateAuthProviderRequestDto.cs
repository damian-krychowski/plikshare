namespace PlikShare.AuthProviders.Update.Contracts;

public class UpdateAuthProviderRequestDto
{
    public required string Name { get; init; }
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public required string IssuerUrl { get; init; }
}
