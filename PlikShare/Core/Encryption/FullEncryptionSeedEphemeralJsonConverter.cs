using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlikShare.Core.Encryption;

public sealed class FullEncryptionSeedEphemeralJsonConverter : JsonConverter<FullEncryptionSeedEphemeral>
{
    public override FullEncryptionSeedEphemeral Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var raw = reader.GetString()
            ?? throw new JsonException(
                $"Expected non-null string for {nameof(FullEncryptionSeedEphemeral)}.");

        return FullEncryptionSeedEphemeral.Deserialize(raw);
    }

    public override void Write(
        Utf8JsonWriter writer,
        FullEncryptionSeedEphemeral value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Serialize());
    }
}
