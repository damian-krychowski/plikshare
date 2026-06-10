using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlikShare.Core.Queue;

[JsonConverter(typeof(QueueJobTypeJsonConverter))]
public readonly record struct QueueJobType(string Value)
{
    public override string ToString() => Value;
}

public class QueueJobTypeJsonConverter : JsonConverter<QueueJobType>
{
    public override QueueJobType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, QueueJobType value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);

    public override QueueJobType ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetString()!);

    public override void WriteAsPropertyName(Utf8JsonWriter writer, QueueJobType value, JsonSerializerOptions options)
        => writer.WritePropertyName(value.Value);
}
