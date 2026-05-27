using System.IO.Pipelines;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Files.PreSignedLinks.Validation;
using PlikShare.Storages;
using PlikShare.Storages.Encryption.Authorization;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Files.Thumbnails.Generation;

public class DownloadFileConvertedOperation(
    GetFilePreSignedDownloadLinkDetailsQuery getFileDetailsQuery,
    FfmpegService ffmpegService)
{
    public async Task<Result> Execute(
        WorkspaceContext workspace,
        FileExtId parentFileExternalId,
        DownloadImageFormat targetFormat,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        CancellationToken cancellationToken)
    {
        if (!ffmpegService.IsAvailable)
            return new Result(Code: ResultCode.FfmpegUnavailable);

        var parentLookup = getFileDetailsQuery.Execute(
            fileExternalId: parentFileExternalId,
            workspaceEncryptionSession: workspaceEncryptionSession);

        if (parentLookup.Code == GetFilePreSignedDownloadLinkDetailsQuery.ResultCode.NotFound
            || parentLookup.Details?.WorkspaceId != workspace.Id)
        {
            return new Result(Code: ResultCode.ParentNotFound);
        }

        if (!ContentTypeHelper.IsThumbnailable(parentLookup.Details.Extension))
            return new Result(Code: ResultCode.ParentNotThumbnailable);

        var parent = parentLookup.Details;
        var encryptionMode = parent.EncryptionMetadata.ToEncryptionMode(
            workspaceEncryptionSession: workspaceEncryptionSession,
            storageClient: workspace.Storage);

        // Buffer the (decrypted) source into memory. Fine for typical photos; if RAW formats
        // hit this path the in-memory peak could be large — acceptable for v1, future stream
        // pipeline can refactor without changing the public contract.
        await using var storageFile = await workspace.Storage.DownloadFile(
            fileDetails: new DownloadFileDetails(
                FileKey: new FileKey
                {
                    FileExternalId = parent.ExternalId,
                    KeySecretPart = parent.KeySecretPart
                },
                FileSizeInBytes: parent.SizeInBytes,
                EncryptionMode: encryptionMode),
            bucketName: workspace.BucketName,
            cancellationToken: cancellationToken);

        var sourceBuffer = new MemoryStream(capacity: (int)Math.Min(parent.SizeInBytes, int.MaxValue));
        var writer = PipeWriter.Create(sourceBuffer);
        await storageFile.ReadTo(writer, cancellationToken);
        await writer.CompleteAsync();
        var sourceBytes = sourceBuffer.ToArray();

        byte[] outputBytes;

        if (string.Equals(parent.Extension, targetFormat.FileExtension, StringComparison.OrdinalIgnoreCase)
            || (string.Equals(parent.Extension, ".jpeg", StringComparison.OrdinalIgnoreCase)
                && targetFormat == DownloadImageFormat.Jpeg))
        {
            // Source already matches target format — skip ffmpeg, stream the original bytes.
            // Avoids unnecessary re-encode (quality loss for JPEG → JPEG, CPU waste in general).
            outputBytes = sourceBytes;
        }
        else
        {
            outputBytes = await ffmpegService.ConvertImage(
                sourceBytes: sourceBytes,
                targetFormat: targetFormat,
                cancellationToken: cancellationToken);
        }

        return new Result(
            Code: ResultCode.Ok,
            Content: outputBytes,
            ContentType: targetFormat.ContentType,
            DownloadFileName: $"{parent.Name}{targetFormat.FileExtension}");
    }

    public record Result(
        ResultCode Code,
        byte[]? Content = null,
        string? ContentType = null,
        string? DownloadFileName = null);

    public enum ResultCode
    {
        Ok = 0,
        FfmpegUnavailable,
        ParentNotFound,
        ParentNotThumbnailable
    }
}
