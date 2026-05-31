using PlikShare.Files.Id;
using PlikShare.Files.Metadata;

namespace PlikShare.MediaProcessing.Generation;

/// <summary>
/// Top-level outcome payload for a batched <c>process-image-thumbnails</c> job, persisted into
/// <c>qc_queue_completed.qc_result</c>. Wraps one <see cref="FileResult"/> per parent file in
/// the batch — single-file jobs are stored as a one-element list. Lets SSE iterate per-file
/// results inside one completed-job row.
/// </summary>
public class ThumbnailGenerationResult
{
    public required List<FileResult> Files { get; init; }

    public class FileResult
    {
        public required FileExtId ParentFileExternalId { get; init; }
        public required List<GeneratedVariant> GeneratedVariants { get; init; }
        public required List<FailedVariant> FailedVariants { get; init; }
    }

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
