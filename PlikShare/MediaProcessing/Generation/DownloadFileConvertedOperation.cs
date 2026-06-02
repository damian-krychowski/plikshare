using System.IO.Pipelines;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Files.PreSignedLinks.Validation;
using PlikShare.Storages;
using PlikShare.Workspaces.Cache;

namespace PlikShare.MediaProcessing.Generation;

public class DownloadFileConvertedOperation(
    GetFilePreSignedDownloadLinkDetailsQuery getFileDetailsQuery,
    FfmpegService ffmpegService)
{
    /// <summary>
    /// Streams the converted (or, when the source already matches, the original) image directly
    /// into <paramref name="output"/> — typically the HTTP response body — so nothing is buffered
    /// in memory. <paramref name="onMetadataResolved"/> is invoked exactly once, after validation
    /// and right before the body starts, so the caller can set response headers; on any error code
    /// it is never called and <paramref name="output"/> is never touched.
    /// </summary>
    public async Task<Result> Execute(
        WorkspaceContext workspace,
        FileExtId parentFileExternalId,
        DownloadImageFormat targetFormat,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        PipeWriter output,
        Action<ConvertedFileMetadata> onMetadataResolved,
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

        var encryptionMode = workspace.GetFileEncryptionMode(
            fileEncryptionMetadata: parent.EncryptionMetadata,
            workspaceEncryptionSession: workspaceEncryptionSession);

        // Source already matches target format — skip ffmpeg, stream the original bytes back.
        // Avoids an unnecessary re-encode (quality loss for JPEG → JPEG, CPU waste in general).
        var isPassthrough =
            string.Equals(parent.Extension, targetFormat.FileExtension, StringComparison.OrdinalIgnoreCase)
            || (string.Equals(parent.Extension, ".jpeg", StringComparison.OrdinalIgnoreCase)
                && targetFormat == DownloadImageFormat.Jpeg);

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

        // Committed to a 200 from here — let the caller set headers before the body flows.
        onMetadataResolved(new ConvertedFileMetadata(
            ContentType: targetFormat.ContentType,
            DownloadFileName: $"{parent.Name}{targetFormat.FileExtension}"));

        if (isPassthrough)
        {
            // Storage -> response, no re-encode, no buffer.
            await storageFile.ReadTo(
                output, 
                cancellationToken);
        }
        else
        {
            // Storage -> ffmpeg stdin -> ffmpeg stdout -> response, fully streamed.
            await ffmpegService.ConvertImage(
                writeSourceTo: (writer, ct) => storageFile.ReadTo(writer, ct),
                targetFormat: targetFormat,
                destination: output,
                cancellationToken: cancellationToken);
        }

        await output.FlushAsync(cancellationToken);

        return new Result(Code: ResultCode.Ok);
    }

    public record Result(ResultCode Code);

    public sealed record ConvertedFileMetadata(
        string ContentType,
        string DownloadFileName);

    public enum ResultCode
    {
        Ok = 0,
        FfmpegUnavailable,
        ParentNotFound,
        ParentNotThumbnailable
    }
}
