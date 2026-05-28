using PlikShare.Files.Metadata;

namespace PlikShare.Files.Thumbnails.Generation.Contracts;

public class GenerateFileThumbnailsRequestDto
{
    public required List<ThumbnailVariant> Variants { get; init; }
}
