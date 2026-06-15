using System.Text.Json.Serialization;
using PlikShare.Core.ExternalIds;
using PlikShare.Core.Utils;

namespace PlikShare.Agents.Id;

[JsonConverter(typeof(ExternalIdJsonConverter<AgentExtId>))]
public readonly record struct AgentExtId(string Value): IExternalId<AgentExtId>
{
    public static string Prefix => "a_";

    public string Value { get; } = !string.IsNullOrWhiteSpace(Value) && Value.StartsWith(Prefix)
        ? Value
        : throw new ArgumentException($"Value '{Value}' is not a valid {nameof(AgentExtId)}");

    public static AgentExtId Parse(string value) => new(value);
    public static AgentExtId NewId() => new($"{Prefix}{Guid.NewGuid().ToBase62()}");

    public static AgentExtId Parse(string s, IFormatProvider? provider) => new(s);

    public static bool TryParse(string? s, out AgentExtId result) => TryParse(s, null, out result);

    public static bool TryParse(string? s, IFormatProvider? provider, out AgentExtId result)
    {
        if (string.IsNullOrWhiteSpace(s) || !s.StartsWith(Prefix))
        {
            result = default;
            return false;
        }

        result = new AgentExtId(s);
        return true;
    }

    public override string ToString() => Value;
}
