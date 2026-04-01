namespace PlikShare.AuthProviders.TestConfiguration.Contracts;

public class TestAuthProviderConfigurationRequestDto
{
    public required string IssuerUrl { get; init; }
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
}

public class TestAuthProviderConfigurationResponseDto
{
    public required string Code { get; init; }
    public required string Details { get; init; }
}
