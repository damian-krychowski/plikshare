using PlikShare.Core.ExternalIds;
using PlikShare.Core.Utils;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace PlikShare.BoxLinks.Id;

[ImmutableObject(true)]
[JsonConverter(typeof(ExternalIdJsonConverter<BoxLinkExtId>))]
public readonly record struct BoxLinkExtId(string Value): IExternalId<BoxLinkExtId>
{
    public static string Prefix => "bl_";

    public string Value { get; } = !string.IsNullOrWhiteSpace(Value) && Value.StartsWith(Prefix)
        ? Value
        : throw new ArgumentException($"Value '{Value}' is not a valid {nameof(BoxLinkExtId)}");
        
    public static BoxLinkExtId Parse(string value) => new(value);
    public static BoxLinkExtId NewId() => new($"{Prefix}{Guid.NewGuid().ToBase62()}");

    public static BoxLinkExtId Parse(string s, IFormatProvider? provider) => new(s);

    public static bool TryParse(string? s, IFormatProvider? provider, out BoxLinkExtId result)
    {
        if (string.IsNullOrWhiteSpace(s) || !s.StartsWith(Prefix))
        {
            result = default;
            return false;
        }

        result = new BoxLinkExtId(s);
        return true;
    }
    
    public override string ToString() => Value;
}