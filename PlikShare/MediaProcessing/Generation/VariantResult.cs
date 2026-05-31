using PlikShare.Files.Metadata;

namespace PlikShare.MediaProcessing.Generation;

/// <summary>
/// Per-variant outcome from a thumbnail generator. Exactly one of <see cref="Thumbnail"/> and
/// <see cref="Error"/> is non-null. The caller disposes <see cref="Thumbnail"/> via
/// <c>await using</c> after consuming its content.
/// </summary>
public sealed record VariantResult(
    ThumbnailVariant Variant,
    IThumbnail? Thumbnail,
    string? Error);
