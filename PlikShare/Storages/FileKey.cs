using System.Text.Json;
using System.Text.Json.Serialization;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;

namespace PlikShare.Storages;

public sealed class FileKey
{
    public required FileExtId FileExternalId { get; init; }
    public required string KeySecretPart { get; init; }

    public string Value => $"{FileExternalId.Value}_{KeySecretPart}";

    public static FileKey NewKey(string? secretPart = default)
    {
        return new FileKey
        {
            FileExternalId = FileExtId.NewId(),
            KeySecretPart = secretPart ?? Guid.NewGuid().ToBase62()
        };
    }
}

public class FileKeyJsonConverter : JsonConverter<FileKey>
{
    public override FileKey? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Unexpected token type when parsing FileKey");

        var value = reader.GetString()!;
        var lastSeparatorIndex = value.LastIndexOf('_');

        if (lastSeparatorIndex == -1)
            throw new JsonException("Invalid FileKey format");

        return new FileKey
        {
            FileExternalId = new FileExtId(value[..lastSeparatorIndex]),
            KeySecretPart = value[(lastSeparatorIndex + 1)..]
        };
    }

    public override void Write(Utf8JsonWriter writer, FileKey? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value);
    }
}
