namespace PlikShare.Files.Metadata;

public class ThumbnailFileMetadata : FileMetadata
{
    public required ThumbnailVariant Variant { get; init; }
}

public enum ThumbnailVariant
{
    Small = 0,
    Large = 1
}
