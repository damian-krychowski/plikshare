using PlikShare.Files.Metadata;

namespace PlikShare.MediaProcessing;

public sealed class MediaProcessingPolicy
{
    public ImageDimensionsSettings? ImageDimensions { get; init; }
    public ThumbnailsSettings? Thumbnails { get; init; }

    public bool ExtractImageDimensionsOnUpload =>
        ImageDimensions?.ExtractOnUpload == true;

    public bool GenerateThumbnailsOnUpload =>
        Thumbnails is { GenerateOnUpload: true, Variants.Length: > 0 };

    public sealed class ImageDimensionsSettings
    {
        public required bool ExtractOnUpload { get; init; }
    }

    public sealed class ThumbnailsSettings
    {
        public required bool GenerateOnUpload { get; init; }
        public required ThumbnailVariant[] Variants { get; init; }
    }
}
