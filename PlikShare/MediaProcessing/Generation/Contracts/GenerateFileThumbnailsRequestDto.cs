using PlikShare.Files.Metadata;

namespace PlikShare.MediaProcessing.Generation.Contracts;

public class GenerateFileThumbnailsRequestDto
{
    public required List<ThumbnailVariant> Variants { get; init; }
}
