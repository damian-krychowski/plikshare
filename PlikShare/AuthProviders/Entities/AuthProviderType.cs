namespace PlikShare.AuthProviders.Entities;

public record AuthProviderType
{
    public string Value { get; }

    private AuthProviderType(string value)
    {
        Value = value;
    }

    public static AuthProviderType Oidc { get; } = new("oidc");

    public static AuthProviderType Build(string type)
    {
        return type switch
        {
            "oidc" => Oidc,
            _ => throw new InvalidOperationException($"Unknown type '{type}' of Auth Provider.")
        };
    }
}
