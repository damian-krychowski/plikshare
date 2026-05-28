using PlikShare.Files.Metadata;

namespace PlikShare.Files.Thumbnails.Generation.Contracts;

/// <summary>
/// Per-file thumbnail generation status, derived purely from the queue tables (q_queue +
/// qc_queue_completed). Because it reads from the database rather than client state, it survives
/// page reloads — the UI can re-discover an in-flight generation after a refresh.
/// </summary>
public class ThumbnailGenerationStatusResponseDto
{
    /// <summary>Variants with a pending/processing job right now (single-file view).</summary>
    public required List<ThumbnailVariant> GeneratingVariants { get; init; }

    /// <summary>
    /// Variants whose most recent completed generation failed (and which are not currently being
    /// regenerated). Each carries the recorded error for display (single-file view).
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
}

public class FailedThumbnailVariantDto
{
    public required ThumbnailVariant Variant { get; init; }
    public required string Error { get; init; }
}
