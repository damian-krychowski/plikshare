using System.ComponentModel;
using System.Text.Json.Serialization;
using PlikShare.Core.ExternalIds;
using PlikShare.Core.Utils;

namespace PlikShare.Boxes.Id;

[ImmutableObject(true)]
[JsonConverter(typeof(ExternalIdJsonConverter<BoxExtId>))]
public readonly record struct BoxExtId(string Value): IExternalId<BoxExtId>
{
    public static string Prefix => "bo_";

    public string Value { get; } = !string.IsNullOrWhiteSpace(Value) && Value.StartsWith(Prefix)
        ? Value
        : throw new ArgumentException($"Value '{Value}' is not a valid {nameof(BoxExtId)}");
        
    public static BoxExtId Parse(string value) => new(value);
    public static BoxExtId NewId() => new($"{Prefix}{Guid.NewGuid().ToBase62()}");

    public static BoxExtId Parse(string s, IFormatProvider? provider) => new(s);

    public static bool TryParse(string? s, IFormatProvider? provider, out BoxExtId result)
    {
        if (s is null)
        {
            result = new BoxExtId();
            return false;   
        }

        result = new BoxExtId(s);
        return true;
    }
    
    public override string ToString() => Value;
}
