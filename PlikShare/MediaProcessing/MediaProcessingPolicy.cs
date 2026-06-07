namespace PlikShare.MediaProcessing;

public sealed class MediaProcessingPolicy
{
    public ImageDimensionsSettings? ImageDimensions { get; init; }

    public bool ExtractImageDimensionsOnUpload =>
        ImageDimensions?.ExtractOnUpload == true;

    public sealed class ImageDimensionsSettings
    {
        public required bool ExtractOnUpload { get; init; }
    }
}
