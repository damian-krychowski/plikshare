using PlikShare.Files.Metadata;

namespace PlikShare.Files.Thumbnails.Generation.Contracts;

/// <summary>
/// Per-file thumbnail generation status, derived purely from the queue tables (q_queue +
/// qc_queue_completed). Because it reads from the database rather than client state, it survives
/// page reloads — the UI can re-discover an in-flight generation after a refresh.
/// </summary>
public class ThumbnailGenerationStatusResponseDto
{
    /// <summary>Variants with a pending/processing job right now.</summary>
    public required List<ThumbnailVariant> GeneratingVariants { get; init; }

    /// <summary>
    /// Variants whose most recent completed generation failed (and which are not currently being
    /// regenerated). Each carries the recorded error for display.
    /// </summary>
    public required List<FailedThumbnailVariantDto> FailedVariants { get; init; }
}

public class FailedThumbnailVariantDto
{
    public required ThumbnailVariant Variant { get; init; }
    public required string Error { get; init; }
}
