namespace PlikShare.Files.Metadata;

public class ImageDimensionsFileMetadata : FileMetadata
{
    public const string TypeDiscriminator = "image-dimensions";

    public required int Width { get; init; }
    public required int Height { get; init; }
}
