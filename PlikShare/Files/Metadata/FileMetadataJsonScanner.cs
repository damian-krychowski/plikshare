using System.Buffers;
using System.Text;
using System.Text.Json;

namespace PlikShare.Files.Metadata;

public static class FileMetadataJsonScanner
{
    private static readonly byte[] ThumbnailDiscriminator =
        Encoding.UTF8.GetBytes(ThumbnailFileMetadata.TypeDiscriminator);

    private static readonly byte[] ImageDimensionsDiscriminator =
        Encoding.UTF8.GetBytes(ImageDimensionsFileMetadata.TypeDiscriminator);

    private static readonly byte[] EtagPropertyName =
        Encoding.UTF8.GetBytes(JsonNamingPolicy.CamelCase.ConvertName(nameof(ThumbnailFileMetadata.Etag)));

    private static readonly byte[] VariantPropertyName =
        Encoding.UTF8.GetBytes(JsonNamingPolicy.CamelCase.ConvertName(nameof(ThumbnailFileMetadata.Variant)));

    private static readonly byte[] WidthPropertyName =
        Encoding.UTF8.GetBytes(JsonNamingPolicy.CamelCase.ConvertName(nameof(ImageDimensionsFileMetadata.Width)));

    private static readonly byte[] HeightPropertyName =
        Encoding.UTF8.GetBytes(JsonNamingPolicy.CamelCase.ConvertName(nameof(ImageDimensionsFileMetadata.Height)));

    private static readonly byte[][] VariantNamesByValue = Enum
        .GetNames<ThumbnailVariant>()
        .Select(Encoding.UTF8.GetBytes)
        .ToArray();

    private static readonly byte[][] VariantCamelCaseNamesByValue = Enum
        .GetNames<ThumbnailVariant>()
        .Select(name => Encoding.UTF8.GetBytes(JsonNamingPolicy.CamelCase.ConvertName(name)))
        .ToArray();

    public readonly record struct ImageDimensions(int Width, int Height);

    public static string? GetThumbnailMiniEtag(string metadataJson)
    {
        return GetThumbnailEtag(
            metadataJson,
            ThumbnailVariant.Mini);
    }

    public static string? GetThumbnailEtag(
        string metadataJson,
        ThumbnailVariant variant)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(
            Encoding.UTF8.GetMaxByteCount(metadataJson.Length));

        try
        {
            var length = Encoding.UTF8.GetBytes(
                metadataJson,
                buffer);

            var reader = new Utf8JsonReader(
                buffer.AsSpan(0, length));

            string? etag = null;
            var isRequestedVariant = false;

            while (reader.Read())
            {
                if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != 1)
                    continue;

                if (reader.ValueTextEquals("$type"u8))
                {
                    reader.Read();

                    if (!reader.ValueTextEquals(ThumbnailDiscriminator))
                        return null;
                }
                else if (reader.ValueTextEquals(VariantPropertyName))
                {
                    reader.Read();

                    isRequestedVariant = IsVariant(ref reader, variant);
                }
                else if (reader.ValueTextEquals(EtagPropertyName))
                {
                    reader.Read();

                    if (reader.TokenType == JsonTokenType.String)
                        etag = reader.GetString();
                }
                else
                {
                    reader.Read();
                    reader.Skip();
                }
            }

            return isRequestedVariant ? etag : null;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static ImageDimensions? GetImageDimensions(string metadataJson)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(
            Encoding.UTF8.GetMaxByteCount(metadataJson.Length));

        try
        {
            var length = Encoding.UTF8.GetBytes(
                metadataJson,
                buffer);

            var reader = new Utf8JsonReader(
                buffer.AsSpan(0, length));

            int? width = null;
            int? height = null;

            while (reader.Read())
            {
                if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != 1)
                    continue;

                if (reader.ValueTextEquals("$type"u8))
                {
                    reader.Read();

                    if (!reader.ValueTextEquals(ImageDimensionsDiscriminator))
                        return null;
                }
                else if (reader.ValueTextEquals(WidthPropertyName))
                {
                    reader.Read();

                    if (reader.TokenType == JsonTokenType.Number)
                        width = reader.GetInt32();
                }
                else if (reader.ValueTextEquals(HeightPropertyName))
                {
                    reader.Read();

                    if (reader.TokenType == JsonTokenType.Number)
                        height = reader.GetInt32();
                }
                else
                {
                    reader.Read();
                    reader.Skip();
                }
            }

            return width is { } w && height is { } h
                ? new ImageDimensions(w, h)
                : null;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static bool IsVariant(ref Utf8JsonReader reader, ThumbnailVariant variant)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.ValueTextEquals(VariantNamesByValue[(int)variant])
                || reader.ValueTextEquals(VariantCamelCaseNamesByValue[(int)variant]);
        }

        if (reader.TokenType == JsonTokenType.Number)
            return reader.GetInt32() == (int)variant;

        return false;
    }
}
