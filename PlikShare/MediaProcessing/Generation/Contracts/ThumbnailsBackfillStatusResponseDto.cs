namespace PlikShare.MediaProcessing.Generation.Contracts;

// Snapshot of a workspace's in-progress thumbnail generation. BatchId is null when nothing is
// currently running; the counts are then all zero.
public class ThumbnailsBackfillStatusResponseDto
{
    public required string? BatchId { get; init; }
    public required int Total { get; init; }
    public required int Completed { get; init; }
    public required int Failed { get; init; }
    public required int Pending { get; init; }
}
