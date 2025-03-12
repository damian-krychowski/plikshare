using System.ComponentModel;
using System.Text.Json.Serialization;
using PlikShare.Core.ExternalIds;
using PlikShare.Core.Utils;

namespace PlikShare.Integrations.Aws.Textract.Id;

[ImmutableObject(true)]
[JsonConverter(typeof(ExternalIdJsonConverter<TextractJobExtId>))]
public readonly record struct TextractJobExtId(string Value): IExternalId<TextractJobExtId>
{
    public static string Prefix => "itj_";

    public string Value { get; } = !string.IsNullOrWhiteSpace(Value) && Value.StartsWith(Prefix)
        ? Value
        : throw new ArgumentException($"Value '{Value}' is not a valid {nameof(TextractJobExtId)}");
        
    public static TextractJobExtId Parse(string value) => new(value);
    public static TextractJobExtId NewId() => new($"{Prefix}{Guid.NewGuid().ToBase62()}");

    public static TextractJobExtId Parse(string s, IFormatProvider? provider) => new(s);

    public static bool TryParse(string? s, out TextractJobExtId result) => TryParse(s, null, out result);

    public static bool TryParse(string? s, IFormatProvider? provider, out TextractJobExtId result)
    {
        if (!string.IsNullOrWhiteSpace(s) && s.StartsWith(Prefix))
        { 
            result = new TextractJobExtId(s);
            return true;
        }
        
        result = new TextractJobExtId();
        return false;   
    }
    
    public override string ToString() => Value;
}