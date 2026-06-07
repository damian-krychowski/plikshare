namespace PlikShare.Files.Metadata;

public class ImageDimensionsFileMetadata : FileMetadata
{
    public required int Width { get; init; }
    public required int Height { get; init; }
}
