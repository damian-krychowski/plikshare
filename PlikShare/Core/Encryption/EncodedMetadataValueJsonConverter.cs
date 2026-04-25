using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlikShare.Core.Encryption;

/// <summary>
/// Round-trip JSON converter for <see cref="EncodedMetadataValue"/>: serializes the
/// inner string verbatim, deserializes a JSON string back into a wrapped value.
///
/// Used implicitly anywhere <c>Json.Options</c> drives serialization — cache (HybridCache),
/// audit log <c>al_details</c>, bulk-insert JSON parameters. The cached/persisted shape
/// stays a plain JSON string, so the column/cache contents look identical to the prior
/// <c>string</c>-typed world; only the C# type carries the extra "this is wire form" signal.
/// </summary>
public sealed class EncodedMetadataValueJsonConverter : JsonConverter<EncodedMetadataValue>
{
    public override EncodedMetadataValue Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var raw = reader.GetString()
            ?? throw new JsonException(
                $"Expected non-null string for {nameof(EncodedMetadataValue)}.");

        return new EncodedMetadataValue(raw);
    }

    public override void Write(
        Utf8JsonWriter writer,
        EncodedMetadataValue value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Encoded);
    }
}
