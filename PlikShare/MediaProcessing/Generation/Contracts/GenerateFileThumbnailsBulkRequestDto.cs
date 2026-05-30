using PlikShare.Files.Id;
using PlikShare.Files.Metadata;

namespace PlikShare.MediaProcessing.Generation.Contracts;

public class GenerateFileThumbnailsBulkRequestDto
{
    public required List<FileExtId> FileExternalIds { get; init; }
    public required List<ThumbnailVariant> Variants { get; init; }
}
