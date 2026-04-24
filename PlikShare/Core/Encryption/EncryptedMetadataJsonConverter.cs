using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlikShare.Core.Encryption;

/// <summary>
/// Stateful <see cref="JsonConverter{T}"/> that decrypts a single metadata string during
/// deserialization. Not attached via <c>[JsonConverter]</c> — instead <see cref="EncryptedMetadataJsonOptions"/>
/// installs an instance of this converter onto every property marked with
/// <see cref="EncryptedMetadataAttribute"/> via the type-info resolver modifier.
///
/// Write path is a straight passthrough; encryption on write happens via
/// <c>EncryptableMetadata.Encode</c> before values reach the serializer.
/// </summary>
public sealed class EncryptedMetadataJsonConverter(
    WorkspaceEncryptionSession? session
) : JsonConverter<string>
{
    public override string? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        var raw = reader.GetString();

        return raw is null
            ? null
            : session.DecodeEncryptableMetadata(raw);
    }

    public override void Write(
        Utf8JsonWriter writer,
        string value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
