using System.Text.Json;
using FluentAssertions;
using PlikShare.Files.Id;
using PlikShare.Storages;

namespace PlikShare.Tests;

public class S3FileKeyJsonConverterTests
{
    private readonly JsonSerializerOptions _options = new()
    {
        Converters = { new S3FileKeyJsonConverter() }
    };

    [Fact]
    public void serialize_null_value_writes_null()
    {
        //given
        S3FileKey? key = null;
        //when
        string json = JsonSerializer.Serialize(key, _options);
        //then
        json.Should().Be("null");
    }

    [Fact]
    public void deserialize_null_value_returns_null()
    {
        //given
        string json = "null";
        //when
        var key = JsonSerializer.Deserialize<S3FileKey>(json, _options);
        //then
        key.Should().BeNull();
    }

    [Fact]
    public void serialize_valid_key_writes_correct_string()
    {
        //given
        var key = new S3FileKey
        {
            FileExternalId = FileExtId.NewId(),
            S3KeySecretPart = "abc"
        };
        //when
        string json = JsonSerializer.Serialize(key, _options);
        //then
        json.Should().Be($"""
                          "{key.FileExternalId.Value}_abc"
                          """);
    }

    [Fact]
    public void deserialize_valid_string_returns_correct_key()
    {
        //given
        var fileId = FileExtId.NewId();
        string json = $"""
                       "{fileId.Value}_abc"
                       """;
        //when
        var key = JsonSerializer.Deserialize<S3FileKey>(json, _options);
        //then
        key.Should().NotBeNull();
        key!.FileExternalId.Value.Should().Be(fileId.Value);
        key.S3KeySecretPart.Should().Be("abc");
    }

    [Fact]
    public void serialize_empty_secret_part_writes_with_trailing_underscore()
    {
        //given
        var key = new S3FileKey
        {
            FileExternalId = FileExtId.NewId(),
            S3KeySecretPart = ""
        };
        //when
        string json = JsonSerializer.Serialize(key, _options);
        //then
        json.Should().Be($"""
                          "{key.FileExternalId.Value}_"
                          """);
    }

    [Fact]
    public void deserialize_empty_secret_part_returns_key_with_empty_secret()
    {
        //given
        var fileId = FileExtId.NewId();
        string json = $"""
                       "{fileId.Value}_"
                       """;
        //when
        var key = JsonSerializer.Deserialize<S3FileKey>(json, _options);
        //then
        key.Should().NotBeNull();
        key!.FileExternalId.Value.Should().Be(fileId.Value);
        key.S3KeySecretPart.Should().Be("");
    }

    [Fact]
    public void deserialize_invalid_json_type_throws_exception()
    {
        //given
        string json = "123";
        //when
        Action action = () => JsonSerializer.Deserialize<S3FileKey>(json, _options);
        //then
        action.Should().Throw<JsonException>()
            .WithMessage("Unexpected token type when parsing S3FileKey");
    }

    [Fact]
    public void deserialize_invalid_format_throws_exception()
    {
        //given
        string json = """
                      "invalid-format"
                      """;
        //when
        Action action = () => JsonSerializer.Deserialize<S3FileKey>(json, _options);
        //then
        action.Should().Throw<JsonException>()
            .WithMessage("Invalid S3FileKey format");
    }

    [Fact]
    public void round_trip_complex_key_preserves_all_data()
    {
        //given
        var originalKey = S3FileKey.NewKey();

        //when
        string json = JsonSerializer.Serialize(originalKey, _options);
        var deserializedKey = JsonSerializer.Deserialize<S3FileKey>(json, _options);
        //then
        deserializedKey.Should().NotBeNull();
        deserializedKey!.Value.Should().Be(originalKey.Value);
        deserializedKey.FileExternalId.Value.Should().Be(originalKey.FileExternalId.Value);
        deserializedKey.S3KeySecretPart.Should().Be(originalKey.S3KeySecretPart);
    }
}