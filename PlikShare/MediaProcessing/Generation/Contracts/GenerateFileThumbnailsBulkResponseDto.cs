namespace PlikShare.MediaProcessing.Generation.Contracts;

public class GenerateFileThumbnailsBulkResponseDto
{
    public required Guid BatchId { get; init; }
    public required int TotalFiles { get; init; }
}
