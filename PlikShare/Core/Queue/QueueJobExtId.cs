using System.Text.Json.Serialization;
using PlikShare.Core.ExternalIds;
using PlikShare.Core.Utils;

namespace PlikShare.Core.Queue;

[JsonConverter(typeof(ExternalIdJsonConverter<QueueJobExtId>))]
public readonly record struct QueueJobExtId(string Value): IExternalId<QueueJobExtId>
{
    public static string Prefix => "q_";

    public string Value { get; } = !string.IsNullOrWhiteSpace(Value) && Value.StartsWith(Prefix)
        ? Value
        : throw new ArgumentException($"Value '{Value}' is not a valid {nameof(QueueJobExtId)}");

    public static QueueJobExtId Parse(string value) => new(value);
    public static QueueJobExtId NewId() => new($"{Prefix}{Guid.NewGuid().ToBase62()}");

    public static QueueJobExtId Parse(string s, IFormatProvider? provider) => new(s);

    public static bool TryParse(string? s, IFormatProvider? provider, out QueueJobExtId result)
    {
        if (string.IsNullOrWhiteSpace(s) || !s.StartsWith(Prefix))
        {
            result = default;
            return false;
        }

        result = new QueueJobExtId(s);
        return true;
    }

    public override string ToString() => Value;
}
