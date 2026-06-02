using System.Buffers.Text;
using System.IO.Pipelines;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Files.Metadata;
using PlikShare.Files.Records;
using PlikShare.Storages;
using PlikShare.Uploads.Algorithm;
using PlikShare.Workspaces.Cache;

namespace PlikShare.MediaProcessing;

/// <summary>
/// Value-object description of one thumbnail attachment being written. Pairs with a separate
/// <see cref="Stream"/> of bytes — the stream's lifecycle stays in the caller's hands and out of
/// this immutable record (same convention as <c>InsertFileAttachmentQuery.AttachmentFile</c>).
/// </summary>
public sealed record ThumbnailDescriptor(
    FileKey FileKey,
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
        FileKey fileKey,
        ThumbnailVariant variant,
        long sizeInBytes) => new(
            FileKey: fileKey,
            Variant: variant,
            SizeInBytes: sizeInBytes,
            ContentType: "image/webp",
            FileName: $"thumb-{variant.ToString().ToLowerInvariant()}",
            FileExtension: ".webp");
}

public static class ThumbnailDescriptorExtensions
{
    extension(ThumbnailDescriptor thumbnail)
    {
        public async Task<string> UploadAndHash(
            WorkspaceContext workspace,
            Stream content,
            FileEncryptionMode encryptionMode,
            CancellationToken cancellationToken)
        {
            var hashingStream = new XxHashingReadStream(
                content);

            await workspace.UploadFilePart(
                input: PipeReader.Create(
                    stream: hashingStream),
                uploadDetails: new UploadFilePartDetails(
                    FileKey: thumbnail.FileKey,
                    MultipartUploadId: null,
                    FileSizeInBytes: thumbnail.SizeInBytes,
                    Part: FilePart.First((int)thumbnail.SizeInBytes),
                    UploadAlgorithm: UploadAlgorithm.DirectUpload,
                    EncryptionMode: encryptionMode),
                cancellationToken: cancellationToken);

            Span<byte> hashBytes = stackalloc byte[16];
            hashingStream.Hash.GetCurrentHash(hashBytes);

            return Base64Url.EncodeToString(hashBytes);
        }
    }
}