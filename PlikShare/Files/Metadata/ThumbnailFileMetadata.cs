namespace PlikShare.Files.Metadata;

public class ThumbnailFileMetadata : FileMetadata
{
    public required ThumbnailVariant Variant { get; init; }
}

public enum ThumbnailVariant
{
    Mini = 0,
    Small = 1,
    Large = 2
}
