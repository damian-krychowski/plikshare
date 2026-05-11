using System.Text.Json.Serialization;
using PlikShare.Core.ExternalIds;
using PlikShare.Core.Utils;

namespace PlikShare.Integrations.Id;

[JsonConverter(typeof(ExternalIdJsonConverter<IntegrationExtId>))]
public readonly record struct IntegrationExtId(string Value): IExternalId<IntegrationExtId>
{
    public static string Prefix => "i_";

    public string Value { get; } = !string.IsNullOrWhiteSpace(Value) && Value.StartsWith(Prefix)
        ? Value
        : throw new ArgumentException($"Value '{Value}' is not a valid {nameof(IntegrationExtId)}");
        
    public static IntegrationExtId Parse(string value) => new(value);
    public static IntegrationExtId NewId() => new($"{Prefix}{Guid.NewGuid().ToBase62()}");

    public static IntegrationExtId Parse(string s, IFormatProvider? provider) => new(s);

    public static bool TryParse(string? s, IFormatProvider? provider, out IntegrationExtId result)
    {
        if (string.IsNullOrWhiteSpace(s) || !s.StartsWith(Prefix))
        {
            result = default;
            return false;
        }

        result = new IntegrationExtId(s);
        return true;
    }
    
    public override string ToString() => Value;
}
