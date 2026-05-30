using PlikShare.Files.Id;
using PlikShare.Files.Metadata;

namespace PlikShare.MediaProcessing.Generation;

/// <summary>
/// Outcome payload for a <c>process-image-thumbnails</c> job, persisted into
/// <c>qc_queue_completed.qc_result</c>. Carries the parent file and each generated variant's etag
/// (so batch-status / SSE can hand the frontend live, reload-safe thumbnail tokens without
/// re-reading or decrypting), plus any per-variant ffmpeg failures — recorded here rather than
/// failing the queue job, so one bad image never blocks the rest of a batch.
/// </summary>
public class ThumbnailGenerationResult
{
    public required FileExtId ParentFileExternalId { get; init; }
    public required List<GeneratedVariant> GeneratedVariants { get; init; }
    public required List<FailedVariant> FailedVariants { get; init; }

    public class GeneratedVariant
    {
        public required ThumbnailVariant Variant { get; init; }
        public required string Etag { get; init; }
    }

    public class FailedVariant
    {
        public required ThumbnailVariant Variant { get; init; }
        public required string Error { get; init; }
    }
}
