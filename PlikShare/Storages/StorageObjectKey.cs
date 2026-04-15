using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;

namespace PlikShare.Storages;

[ImmutableObject(true)]
public class StorageObjectKey
{
    public required FileExtId FileExternalId { get; init; }
    public required string SecretPart { get; init; }

    // compatibility bridge for legacy naming in existing code paths
    public string S3KeySecretPart
    {
        get => SecretPart;
        init => SecretPart = value;
    }

    public string Value => $"{FileExternalId.Value}_{SecretPart}";

    public static StorageObjectKey NewKey(string? secretPart = default)
    {
        return new StorageObjectKey
        {
            FileExternalId = FileExtId.NewId(),
            SecretPart = secretPart ?? Guid.NewGuid().ToBase62()
        };
    }

    public static implicit operator StorageObjectKey(S3FileKey key)
    {
        return new StorageObjectKey
        {
            FileExternalId = key.FileExternalId,
            SecretPart = key.S3KeySecretPart
        };
    }

    public static implicit operator S3FileKey(StorageObjectKey key)
    {
        return new S3FileKey
        {
            FileExternalId = key.FileExternalId,
            S3KeySecretPart = key.SecretPart
        };
    }
}

public class StorageObjectKeyJsonConverter : JsonConverter<StorageObjectKey>
{
    public override StorageObjectKey? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Unexpected token type when parsing StorageObjectKey");

        var value = reader.GetString()!;
        var lastSeparatorIndex = value.LastIndexOf('_');

        if (lastSeparatorIndex == -1)
            throw new JsonException("Invalid StorageObjectKey format");

        return new StorageObjectKey
        {
            FileExternalId = new FileExtId(value[..lastSeparatorIndex]),
            SecretPart = value[(lastSeparatorIndex + 1)..]
        };
    }

    public override void Write(Utf8JsonWriter writer, StorageObjectKey? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value);
    }
}
