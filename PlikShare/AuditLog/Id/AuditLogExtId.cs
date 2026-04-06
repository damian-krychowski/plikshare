using System.ComponentModel;
using System.Text.Json.Serialization;
using PlikShare.Core.ExternalIds;
using PlikShare.Core.Utils;

namespace PlikShare.AuditLog.Id;

[ImmutableObject(true)]
[JsonConverter(typeof(ExternalIdJsonConverter<AuditLogExtId>))]
public readonly record struct AuditLogExtId(string Value) : IExternalId<AuditLogExtId>
{
    public static string Prefix => "al_";

    public string Value { get; } = !string.IsNullOrWhiteSpace(Value) && Value.StartsWith(Prefix)
        ? Value
        : throw new ArgumentException($"Value '{Value}' is not a valid {nameof(AuditLogExtId)}");

    public static AuditLogExtId Parse(string value) => new(value);
    public static AuditLogExtId NewId() => new($"{Prefix}{Guid.NewGuid().ToBase62()}");

    public static AuditLogExtId Parse(string s, IFormatProvider? provider) => new(s);

    public static bool TryParse(string? s, IFormatProvider? provider, out AuditLogExtId result)
    {
        if (s is null)
        {
            result = new AuditLogExtId();
            return false;
        }

        result = new AuditLogExtId(s);
        return true;
    }

    public override string ToString() => Value;
}
