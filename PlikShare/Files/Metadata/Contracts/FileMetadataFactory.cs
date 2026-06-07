namespace PlikShare.Files.Metadata.Contracts;

public static class FileMetadataFactory
{
    public static FileMetadataDto? Prepare(
        ThumbnailMetadataDto? thumbnail,
        DimensionsMetadataDto? dimensions)
    {
        if (thumbnail is null && dimensions is null)
            return null;

        return new FileMetadataDto
        {
            Thumbnail = thumbnail,
            Dimensions = dimensions
        };
    }
}
