using System.ComponentModel;
using System.Text.Json.Serialization;
using PlikShare.Core.ExternalIds;
using PlikShare.Core.Utils;

namespace PlikShare.Core.IdentityProvider;

[ImmutableObject(true)]
[JsonConverter(typeof(ExternalIdJsonConverter<RoleExtId>))]
public readonly record struct RoleExtId(string Value): IExternalId<RoleExtId>
{
    public static string Prefix => "r_";

    public string Value { get; } = !string.IsNullOrWhiteSpace(Value) && Value.StartsWith(Prefix)
        ? Value
        : throw new ArgumentException($"Value '{Value}' is not a valid {nameof(RoleExtId)}");
        
    public static RoleExtId Parse(string value) => new(value);
    public static RoleExtId NewId() => new($"{Prefix}{Guid.NewGuid().ToBase62()}");

    public static RoleExtId Parse(string s, IFormatProvider? provider) => new(s);

    public static bool TryParse(string? s, IFormatProvider? provider, out RoleExtId result)
    {
        if (s is null)
        {
            result = new RoleExtId();
            return false;   
        }

        result = new RoleExtId(s);
        return true;
    }
    
    public override string ToString() => Value;
}