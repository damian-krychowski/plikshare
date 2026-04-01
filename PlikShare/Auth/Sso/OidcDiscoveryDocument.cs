namespace PlikShare.Auth.Sso;

public class OidcDiscoveryDocument
{
    public required string AuthorizationEndpoint { get; init; }
    public required string TokenEndpoint { get; init; }
    public string? UserinfoEndpoint { get; init; }
    public required string JwksUri { get; init; }
    public required string Issuer { get; init; }
}
