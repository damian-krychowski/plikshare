using PlikShare.Files.Metadata;

namespace PlikShare.MediaProcessing.Generation.Contracts;

/// <summary>
/// Per-file thumbnail generation status, derived purely from the queue tables (q_queue +
/// qc_queue_completed). Because it reads from the database rather than client state, it survives
/// page reloads — the UI can re-discover an in-flight generation after a refresh.
/// </summary>
public class ThumbnailGenerationStatusResponseDto
{
    /// <summary>
    /// Variants whose most recent completed generation failed. Each carries the recorded error for
    /// display (single-file view).
    /// </summary>
    public required List<FailedThumbnailVariantDto> FailedVariants { get; init; }

    /// <summary>Total jobs in the batch (per-file progress view): pending + failed + completed.</summary>
    public required int Total { get; init; }

    /// <summary>Jobs finished (moved to the completed archive) — includes partial-success ones.</summary>
    public required int Completed { get; init; }

    /// <summary>Jobs that failed at the infra level (exhausted retries).</summary>
    public required int Failed { get; init; }

    /// <summary>Jobs still outstanding (pending / processing / blocked). Batch is done when 0.</summary>
    public required int Pending { get; init; }

    /// <summary>
    /// Files whose thumbnails finished generating, each with the generated variants' etags. Lets the
    /// frontend show thumbnails live without a reload. Settable so the SSE stream can trim it to a
    /// per-connection delta (only newly-ready files); the one-shot status endpoint returns the full set.
    /// </summary>
    public List<ReadyThumbnailDto> ReadyThumbnails { get; set; } = [];

    /// <summary>
    /// External ids of the parent files still outstanding (pending/processing/blocked). Drives the
    /// per-file "processing" indicator; survives reload because it's recomputed from the queue on
    /// every push, not tracked client-side.
    /// </summary>
    public required List<string> ProcessingFileExternalIds { get; init; }
}

public class FailedThumbnailVariantDto
{
    public required ThumbnailVariant Variant { get; init; }
    public required string Error { get; init; }
}

public class ReadyThumbnailDto
{
    public required string FileExternalId { get; init; }
    public required List<ReadyThumbnailVariantDto> Variants { get; init; }
}

public class ReadyThumbnailVariantDto
{
    public required ThumbnailVariant Variant { get; init; }
    public required string Etag { get; init; }
}
