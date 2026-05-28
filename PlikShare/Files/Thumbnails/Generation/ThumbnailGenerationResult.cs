using PlikShare.Files.Metadata;

namespace PlikShare.Files.Thumbnails.Generation;

/// <summary>
/// Outcome payload for a <c>process-image-thumbnails</c> job, persisted into
/// <c>qc_queue_completed.qc_result</c>. Written only when a variant failed to generate — a fully
/// successful job leaves <c>qc_result</c> NULL (the generated thumbnails themselves are the proof
/// of success). A per-variant ffmpeg failure is recorded here rather than failing the queue job,
/// so one bad image never blocks the rest of a batch; the user is shown the collected failures.
/// </summary>
public class ThumbnailGenerationResult
{
    public required List<FailedVariant> FailedVariants { get; init; }

    public class FailedVariant
    {
        public required ThumbnailVariant Variant { get; init; }
        public required string Error { get; init; }
    }
}
