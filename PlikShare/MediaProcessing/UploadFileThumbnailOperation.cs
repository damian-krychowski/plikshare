using PlikShare.Core.Encryption;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Files.Metadata;
using PlikShare.Files.UploadAttachment;
using PlikShare.Storages;
using PlikShare.Uploads.Algorithm;
using PlikShare.Workspaces.Cache;
using System.Buffers.Text;
using System.IO.Pipelines;
using PlikShare.Files.Records;

namespace PlikShare.MediaProcessing;

/// <summary>
/// Writes one thumbnail attachment to storage and registers it in the DB. The CALLER vouches for
/// parent existence, workspace ownership and thumbnailability — this operation does NOT re-check.
/// Sequence: read existing thumbnails of this variant (1 read, off DbWriteQueue) → storage upload
/// → single DbWriteQueue transaction that inserts the new row as completed AND hard-deletes the
/// old ones. A crash between storage and insert leaves an orphan blob — accepted trade-off
/// (see <see cref="InsertAndFinalizeThumbnailQuery"/>).
/// </summary>
public class UploadFileThumbnailOperation(
    GetThumbnailsQuery getThumbnailsQuery,
    InsertAndFinalizeThumbnailQuery insertAndFinalizeThumbnailQuery)
{
    public async Task<Result> Execute(
        WorkspaceContext workspace,
        FileExtId parentFileExternalId,
        ThumbnailDescriptor thumbnail,
        Stream content,
        IUserIdentity uploader,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        // Snapshot which existing thumbnails of this variant will be replaced. Off DbWriteQueue —
        // this is a read. The replacement happens later, atomically inside the insert+delete tx.
        var existingThumbnails = getThumbnailsQuery.Execute(
            workspace: workspace,
            parentFileExternalId: parentFileExternalId,
            workspaceEncryptionSession: workspaceEncryptionSession);

        var oldThumbnailFileIds = existingThumbnails
            .Where(t => t.Variant == thumbnail.Variant)
            .Select(t => t.Id)
            .ToList();

        // Generate identifiers + encryption parameters in-memory BEFORE the upload — none of them
        // depend on DB state, so we can do them without a round-trip and use them for both the
        // storage write and the final insert.
        var keySecretPart = workspace.Storage.GenerateFileKeySecretPart();

        var encryptionMetadata = workspace.Storage.GenerateFileEncryptionMetadata(
            workspace.EncryptionMetadata);

        var encryptionMode = encryptionMetadata.ToEncryptionMode(
            workspaceEncryptionSession: workspaceEncryptionSession,
            storageClient: workspace.Storage);

        var uploadDetails = new UploadFilePartDetails(
            FileKey: new FileKey
            {
                KeySecretPart = keySecretPart,
                FileExternalId = thumbnail.ExternalId
            },
            MultipartUploadId: null,
            FileSizeInBytes: thumbnail.SizeInBytes,
            Part: FilePart.First((int)thumbnail.SizeInBytes),
            UploadAlgorithm: UploadAlgorithm.DirectUpload,
            EncryptionMode: encryptionMode);

        var hashingStream = new XxHashingReadStream(
            content);

        await workspace.UploadFilePart(
            input: PipeReader.Create(
                stream: hashingStream),
            uploadDetails: uploadDetails,
            cancellationToken: cancellationToken);

        Span<byte> hashBytes = stackalloc byte[16];
        hashingStream.Hash.GetCurrentHash(hashBytes);
        var etag = Base64Url.EncodeToString(hashBytes);

        var thumbnailMetadata = workspaceEncryptionSession.ToEncryptableMetadata(
            Json.Serialize<FileMetadata>(
                new ThumbnailFileMetadata
                {
                    Variant = thumbnail.Variant,
                    Etag = etag
                }));

        var attachment = new InsertFileAttachmentQuery.AttachmentFile
        {
            ExternalId = thumbnail.ExternalId,
            ContentType = workspaceEncryptionSession.ToEncryptableMetadata(
                thumbnail.ContentType),
            Name = workspaceEncryptionSession.ToEncryptableMetadata(
                thumbnail.FileName),
            Extension = workspaceEncryptionSession.ToEncryptableMetadata(
                thumbnail.FileExtension),
            SizeInBytes = thumbnail.SizeInBytes,
            KeySecretPart = keySecretPart,
            EncryptionMetadata = encryptionMetadata,
            Metadata = thumbnailMetadata
        };

        var insertResult = await insertAndFinalizeThumbnailQuery.Execute(
            workspace: workspace,
            parentFileExternalId: parentFileExternalId,
            attachment: attachment,
            oldThumbnailFileIds: oldThumbnailFileIds,
            uploader: uploader,
            correlationId: correlationId,
            cancellationToken: cancellationToken);

        if (insertResult == InsertAndFinalizeThumbnailQuery.ResultCode.ParentNotFound)
            return new Result(Code: ResultCode.ParentNotFound);

        return new Result(
            Code: ResultCode.Ok,
            Attachment: attachment,
            Etag: etag);
    }

    public record Result(
        ResultCode Code,
        InsertFileAttachmentQuery.AttachmentFile? Attachment = null,
        string? Etag = null);

    public enum ResultCode
    {
        Ok = 0,
        ParentNotFound
    }
}
