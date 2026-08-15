using System.Text.Json.Serialization;
using PlikShare.Core.ExternalIds;
using PlikShare.Core.Utils;

namespace PlikShare.Agents.Events.Id;

[JsonConverter(typeof(ExternalIdJsonConverter<AgentEventExtId>))]
public readonly record struct AgentEventExtId(string Value) : IExternalId<AgentEventExtId>
{
    public static string Prefix => "aev_";

    public string Value { get; } = !string.IsNullOrWhiteSpace(Value) && Value.StartsWith(Prefix)
        ? Value
        : throw new ArgumentException($"Value '{Value}' is not a valid {nameof(AgentEventExtId)}");

    public static AgentEventExtId Parse(string value) => new(value);
    public static AgentEventExtId NewId() => new($"{Prefix}{Guid.NewGuid().ToBase62()}");

    public static AgentEventExtId Parse(string s, IFormatProvider? provider) => new(s);

    public static bool TryParse(string? s, out AgentEventExtId result) => TryParse(s, null, out result);

    public static bool TryParse(string? s, IFormatProvider? provider, out AgentEventExtId result)
    {
        if (string.IsNullOrWhiteSpace(s) || !s.StartsWith(Prefix))
        {
            result = default;
            return false;
        }

        result = new AgentEventExtId(s);
        return true;
    }

    public override string ToString() => Value;
}
