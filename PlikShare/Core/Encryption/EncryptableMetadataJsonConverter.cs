using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlikShare.Core.Encryption;

/// <summary>
/// Serializes <see cref="EncryptableMetadata"/> by emitting <see cref="EncryptableMetadataExtensions.Encode"/>:
/// plaintext for <see cref="NoMetadataEncryption"/>, base64 envelope (with <see cref="EncryptableMetadataExtensions.ReservedPrefix"/>)
/// for <see cref="AesGcmV1MetadataEncryption"/>.
///
/// Used at JSON write boundaries that ultimately land in a SQLite TEXT column via
/// <c>json_extract</c> bulk-insert paths (e.g. <c>WithJsonParameter</c> + bulk INSERT). The struct
/// stays typed all the way to the boundary; the converter handles the encode at serialization.
///
/// Read direction throws — reverse path exists but goes through a different mechanism: SQL columns
/// are read as raw strings and decrypted via <see cref="EncryptableMetadataExtensions.DecodeEncryptableMetadata"/>
/// against a per-request <see cref="WorkspaceEncryptionSession"/>. There is no session at JSON
/// deserialization time, so deserializing back into <see cref="EncryptableMetadata"/> is not supported.
/// </summary>
public sealed class EncryptableMetadataJsonConverter : JsonConverter<EncryptableMetadata>
{
    public override EncryptableMetadata Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        throw new NotSupportedException(
            $"Deserializing JSON into {nameof(EncryptableMetadata)} is not supported. " +
            "Read encrypted values as strings and decode via WorkspaceEncryptionSession.DecodeEncryptableMetadata.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        EncryptableMetadata value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Encode().Encoded);
    }
}
