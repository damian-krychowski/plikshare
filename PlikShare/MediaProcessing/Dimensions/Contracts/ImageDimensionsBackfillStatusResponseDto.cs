namespace PlikShare.MediaProcessing.Dimensions.Contracts;

// Snapshot of a workspace's image-dimensions backfill. BatchId is null when no backfill is
// currently in progress; the counts are then all zero.
public class ImageDimensionsBackfillStatusResponseDto
{
    public required string? BatchId { get; init; }
    public required int Total { get; init; }
    public required int Completed { get; init; }
    public required int Failed { get; init; }
    public required int Pending { get; init; }
}
