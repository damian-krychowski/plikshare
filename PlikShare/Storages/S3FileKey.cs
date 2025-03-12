using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;

namespace PlikShare.Storages;

[ImmutableObject(true)]
public sealed class S3FileKey
{
    public required FileExtId FileExternalId { get; init; }
    public required string S3KeySecretPart { get; init; }

    public string Value => $"{FileExternalId.Value}_{S3KeySecretPart}";
    
    public static S3FileKey NewKey(string? secretPart = default)
    {
        return new S3FileKey
        {
            FileExternalId = FileExtId.NewId(),
            S3KeySecretPart = secretPart ?? Guid.NewGuid().ToBase62()
        };
    }
}

public class S3FileKeyJsonConverter : JsonConverter<S3FileKey>
{
    public override S3FileKey? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Unexpected token type when parsing S3FileKey");

        var value = reader.GetString()!;
        var lastSeparatorIndex = value.LastIndexOf('_');

        if (lastSeparatorIndex == -1)
            throw new JsonException("Invalid S3FileKey format");

        return new S3FileKey
        {
            FileExternalId = new FileExtId(value[..lastSeparatorIndex]),
            S3KeySecretPart = value[(lastSeparatorIndex + 1)..]
        };
    }

    public override void Write(Utf8JsonWriter writer, S3FileKey? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value);
    }
}