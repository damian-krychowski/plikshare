using System.ComponentModel;
using System.Text.Json.Serialization;
using PlikShare.Core.ExternalIds;
using PlikShare.Core.Utils;

namespace PlikShare.ArtificialIntelligence.Id;

[ImmutableObject(true)]
[JsonConverter(typeof(ExternalIdJsonConverter<AiConversationExtId>))]
public readonly record struct AiConversationExtId(string Value): IExternalId<AiConversationExtId>
{
    public static string Prefix => "aic_";

    public string Value { get; } = !string.IsNullOrWhiteSpace(Value) && Value.StartsWith(Prefix)
        ? Value
        : throw new ArgumentException($"Value '{Value}' is not a valid {nameof(AiConversationExtId)}");
        
    public static AiConversationExtId Parse(string value) => new(value);
    public static AiConversationExtId NewId() => new($"{Prefix}{Guid.NewGuid().ToBase62()}");

    public static AiConversationExtId Parse(string s, IFormatProvider? provider) => new(s);

    public static bool TryParse(string? s, IFormatProvider? provider, out AiConversationExtId result)
    {
        if (s is null)
        {
            result = new AiConversationExtId();
            return false;   
        }

        result = new AiConversationExtId(s);
        return true;
    }
    
    public override string ToString() => Value;
}