using PlikShare.Files.Id;
using PlikShare.Files.Metadata;
using PlikShare.Users.Id;

namespace PlikShare.MediaProcessing.Generation;

/// <summary>
/// A single queue job processes a BATCH of parent files (1..N) under the same workspace and
/// variant set. Batching cuts the per-file overhead in <c>DbWriteQueue</c> (one final
/// insert+finalize tx for all variants of all files in the batch instead of one per file).
/// </summary>
public class ProcessImageQueueJobDefinition
{
    public required int WorkspaceId { get; init; }

    /// <summary>Per-file entries. One queue job covers <see cref="Files"/>.Count parents.</summary>
    public required List<BatchItem> Files { get; init; }

    /// <summary>Variants generated for every file in the batch.</summary>
    public required List<ThumbnailVariant> Variants { get; init; }

    /// <summary>
    /// User that clicked "Generate" — recorded as the thumbnail's <c>fi_uploader_identity</c>
    /// so attribution survives queue processing. Shared across all files in the batch.
    /// </summary>
    public required UserExtId TriggeredByUserExternalId { get; init; }

    public class BatchItem
    {
        public required FileExtId ParentFileExternalId { get; init; }

        /// <summary>
        /// Parent file's extension (eg. <c>.jpg</c>, <c>.mp4</c>). Captured at enqueue time so the
        /// executor can route image vs video without a second decrypt round-trip per file —
        /// video gets a temp-file ffmpeg path (mp4 demux + thumbnail filter on stdin blows up
        /// memory for non-fast-start moov atoms), image stays on the cheap stdin path.
        /// </summary>
        public required string Extension { get; init; }

        /// <summary>
        /// Handle into <see cref="PlikShare.Core.Encryption.TemporaryEncryptionStore"/> resolving
        /// to a <see cref="PlikShare.Core.Encryption.TemporaryEncryptionStore.Package"/> that
        /// carries every encryption input the worker needs for this parent file: the decryption
        /// input (master-encrypted wire) for the parent body, the encryption inputs (one per
        /// variant) for the thumbnail bodies, and the metadata encryption seed for the per-value
        /// attachment metadata. Null when the parent's workspace uses None or Managed encryption
        /// — the worker handles those without any keystore detour.
        /// </summary>
        public required Guid? TempEncryptionKeyId { get; init; }
    }
}
