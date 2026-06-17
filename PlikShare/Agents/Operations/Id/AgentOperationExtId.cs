using System.Text.Json.Serialization;
using PlikShare.Core.ExternalIds;
using PlikShare.Core.Utils;

namespace PlikShare.Agents.Operations.Id;

[JsonConverter(typeof(ExternalIdJsonConverter<AgentOperationExtId>))]
public readonly record struct AgentOperationExtId(string Value): IExternalId<AgentOperationExtId>
{
    public static string Prefix => "aop_";

    public string Value { get; } = !string.IsNullOrWhiteSpace(Value) && Value.StartsWith(Prefix)
        ? Value
        : throw new ArgumentException($"Value '{Value}' is not a valid {nameof(AgentOperationExtId)}");

    public static AgentOperationExtId Parse(string value) => new(value);
    public static AgentOperationExtId NewId() => new($"{Prefix}{Guid.NewGuid().ToBase62()}");

    public static AgentOperationExtId Parse(string s, IFormatProvider? provider) => new(s);

    public static bool TryParse(string? s, out AgentOperationExtId result) => TryParse(s, null, out result);

    public static bool TryParse(string? s, IFormatProvider? provider, out AgentOperationExtId result)
    {
        if (string.IsNullOrWhiteSpace(s) || !s.StartsWith(Prefix))
        {
            result = default;
            return false;
        }

        result = new AgentOperationExtId(s);
        return true;
    }

    public override string ToString() => Value;
}
