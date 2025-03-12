using System.ComponentModel;
using System.Text.Json.Serialization;
using PlikShare.Core.ExternalIds;
using PlikShare.Core.Utils;

namespace PlikShare.Users.Id;

[ImmutableObject(true)]
[JsonConverter(typeof(ExternalIdJsonConverter<UserExtId>))]
public readonly record struct UserExtId(string Value): IExternalId<UserExtId>
{
    public static string Prefix => "u_";

    public string Value { get; } = !string.IsNullOrWhiteSpace(Value) && Value.StartsWith(Prefix)
        ? Value
        : throw new ArgumentException($"Value '{Value}' is not a valid {nameof(UserExtId)}");
        
    public static UserExtId Parse(string value) => new(value);
    public static UserExtId NewId() => new($"{Prefix}{Guid.NewGuid().ToBase62()}");

    public static UserExtId Parse(string s, IFormatProvider? provider) => new(s);

    public static bool TryParse(string? s, out UserExtId result) => TryParse(s, null, out result);

    public static bool TryParse(string? s, IFormatProvider? provider, out UserExtId result)
    {
        if (!string.IsNullOrWhiteSpace(s) && s.StartsWith(Prefix))
        { 
            result = new UserExtId(s);
            return true;
        }
        
        result = new UserExtId();
        return false;   
    }
    
    public override string ToString() => Value;
}