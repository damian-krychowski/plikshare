using PlikShare.Core.ExternalIds;
using PlikShare.Core.Utils;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace PlikShare.QuickShares.Id;

[ImmutableObject(true)]
[JsonConverter(typeof(ExternalIdJsonConverter<QuickShareExtId>))]
public readonly record struct QuickShareExtId(string Value) : IExternalId<QuickShareExtId>
{
    public static string Prefix => "qs_";

    public string Value { get; } = !string.IsNullOrWhiteSpace(Value) && Value.StartsWith(Prefix)
        ? Value
        : throw new ArgumentException($"Value '{Value}' is not a valid {nameof(QuickShareExtId)}");

    public static QuickShareExtId Parse(string value) => new(value);
    public static QuickShareExtId NewId() => new($"{Prefix}{Guid.NewGuid().ToBase62()}");

    public static QuickShareExtId Parse(string s, IFormatProvider? provider) => new(s);

    public static bool TryParse(string? s, IFormatProvider? provider, out QuickShareExtId result)
    {
        if (string.IsNullOrWhiteSpace(s) || !s.StartsWith(Prefix))
        {
            result = default;
            return false;
        }

        result = new QuickShareExtId(s);
        return true;
    }

    public override string ToString() => Value;
}
