using PlikShare.Files.Id;
using PlikShare.Files.Metadata;

namespace PlikShare.MediaProcessing;

/// <summary>
/// Value-object description of one thumbnail attachment being written. Pairs with a separate
/// <see cref="Stream"/> of bytes — the stream's lifecycle stays in the caller's hands and out of
/// this immutable record (same convention as <c>InsertFileAttachmentQuery.AttachmentFile</c>).
/// </summary>
public sealed record ThumbnailDescriptor(
    FileExtId ExternalId,
    ThumbnailVariant Variant,
    long SizeInBytes,
    string ContentType,
    string FileName,
    string FileExtension)
{
    /// <summary>
    /// Shape used by every WebP thumbnail produced by the queue executor — same content type,
    /// same per-variant filename, same extension. One factory so the executor doesn't repeat the
    /// trio at each call site (or drift if we ever change them).
    /// </summary>
    public static ThumbnailDescriptor ForGeneratedWebp(
        FileExtId externalId,
        ThumbnailVariant variant,
        long sizeInBytes) => new(
            ExternalId: externalId,
            Variant: variant,
            SizeInBytes: sizeInBytes,
            ContentType: "image/webp",
            FileName: $"thumb-{variant.ToString().ToLowerInvariant()}",
            FileExtension: ".webp");
}
