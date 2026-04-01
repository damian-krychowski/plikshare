using System.ComponentModel;
using System.Text.Json.Serialization;
using PlikShare.Core.ExternalIds;
using PlikShare.Core.Utils;

namespace PlikShare.AuthProviders.Id;

[ImmutableObject(true)]
[JsonConverter(typeof(ExternalIdJsonConverter<AuthProviderExtId>))]
public readonly record struct AuthProviderExtId(string Value): IExternalId<AuthProviderExtId>
{
    public static string Prefix => "ap_";

    public string Value { get; } = !string.IsNullOrWhiteSpace(Value) && Value.StartsWith(Prefix)
        ? Value
        : throw new ArgumentException($"Value '{Value}' is not a valid {nameof(AuthProviderExtId)}");

    public static AuthProviderExtId Parse(string value) => new(value);
    public static AuthProviderExtId NewId() => new($"{Prefix}{Guid.NewGuid().ToBase62()}");

    public static AuthProviderExtId Parse(string s, IFormatProvider? provider) => new(s);

    public static bool TryParse(string? s, IFormatProvider? provider, out AuthProviderExtId result)
    {
        if (s is null)
        {
            result = new AuthProviderExtId();
            return false;
        }

        result = new AuthProviderExtId(s);
        return true;
    }

    public override string ToString() => Value;
}
