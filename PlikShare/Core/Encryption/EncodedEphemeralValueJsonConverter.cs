using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlikShare.Core.Encryption;

public sealed class EncodedEphemeralValueJsonConverter : JsonConverter<EncodedEphemeralValue>
{
    public override EncodedEphemeralValue Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var raw = reader.GetString()
            ?? throw new JsonException(
                $"Expected non-null string for {nameof(EncodedEphemeralValue)}.");

        return new EncodedEphemeralValue(raw);
    }

    public override void Write(
        Utf8JsonWriter writer,
        EncodedEphemeralValue value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Encoded);
    }
}
