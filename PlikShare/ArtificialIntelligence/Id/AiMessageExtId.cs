using System.ComponentModel;
using System.Text.Json.Serialization;
using PlikShare.Core.ExternalIds;
using PlikShare.Core.Utils;

namespace PlikShare.ArtificialIntelligence.Id;

[ImmutableObject(true)]
[JsonConverter(typeof(ExternalIdJsonConverter<AiMessageExtId>))]
public readonly record struct AiMessageExtId(string Value): IExternalId<AiMessageExtId>
{
    public static string Prefix => "aim_";

    public string Value { get; } = !string.IsNullOrWhiteSpace(Value) && Value.StartsWith(Prefix)
        ? Value
        : throw new ArgumentException($"Value '{Value}' is not a valid {nameof(AiMessageExtId)}");
        
    public static AiMessageExtId Parse(string value) => new(value);
    public static AiMessageExtId NewId() => new($"{Prefix}{Guid.NewGuid().ToBase62()}");

    public static AiMessageExtId Parse(string s, IFormatProvider? provider) => new(s);

    public static bool TryParse(string? s, IFormatProvider? provider, out AiMessageExtId result)
    {
        if (s is null)
        {
            result = new AiMessageExtId();
            return false;   
        }

        result = new AiMessageExtId(s);
        return true;
    }
    
    public override string ToString() => Value;
}