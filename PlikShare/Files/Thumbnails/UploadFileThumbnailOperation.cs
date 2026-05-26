using System.IO.Pipelines;
using PlikShare.Core.Encryption;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Files.Metadata;
using PlikShare.Files.PreSignedLinks.Validation;
using PlikShare.Files.Records;
using PlikShare.Files.UploadAttachment;
using PlikShare.Storages;
using PlikShare.Uploads.Algorithm;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Files.Thumbnails;

public class UploadFileThumbnailOperation(
    GetFilePreSignedDownloadLinkDetailsQuery getParentFileDetailsQuery,
    GetThumbnailsQuery getThumbnailsQuery,
    InsertFileAttachmentQuery insertFileAttachmentQuery,
    FinalizeThumbnailUploadQuery finalizeThumbnailUploadQuery)
{
    public async Task<Result> Execute(
        WorkspaceContext workspace,
        FileExtId parentFileExternalId,
        FileExtId thumbnailFileExternalId,
        ThumbnailVariant variant,
        Stream thumbnailContent,
        long thumbnailSizeInBytes,
        string thumbnailContentType,
        string thumbnailFileName,
        string thumbnailFileExtension,
        IUserIdentity uploader,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var parentLookup = getParentFileDetailsQuery.Execute(
            fileExternalId: parentFileExternalId,
            workspaceEncryptionSession: workspaceEncryptionSession);

        if (parentLookup.Code == GetFilePreSignedDownloadLinkDetailsQuery.ResultCode.NotFound
            || parentLookup.Details?.WorkspaceId != workspace.Id)
        {
            return new Result(Code: ResultCode.ParentNotFound);
        }

        if (!ContentTypeHelper.IsThumbnailable(parentLookup.Details.Extension))
            return new Result(Code: ResultCode.ParentNotThumbnailable);

        // Snapshot which existing thumbnails of this variant will be replaced. We don't
        // touch them yet — if the storage upload below fails the new row stays incomplete
        // and the old thumb keeps serving reads.
        var existingThumbnails = getThumbnailsQuery.Execute(
            workspace: workspace,
            parentFileExternalId: parentFileExternalId,
            workspaceEncryptionSession: workspaceEncryptionSession);

        var oldThumbnailFileIds = existingThumbnails
            .Where(t => t.Variant == variant)
            .Select(t => t.Id)
            .ToList();

        var thumbnailMetadataJson = Json.Serialize<FileMetadata>(
            new ThumbnailFileMetadata
            {
                Variant = variant
            });

        var attachment = new InsertFileAttachmentQuery.AttachmentFile
        {
            ExternalId = thumbnailFileExternalId,
            ContentType = workspaceEncryptionSession.ToEncryptableMetadata(thumbnailContentType),
            Name = workspaceEncryptionSession.ToEncryptableMetadata(thumbnailFileName),
            Extension = workspaceEncryptionSession.ToEncryptableMetadata(thumbnailFileExtension),
            SizeInBytes = thumbnailSizeInBytes,
            KeySecretPart = workspace.Storage.GenerateFileKeySecretPart(),
            EncryptionMetadata = workspace.Storage.GenerateFileEncryptionMetadata(
                workspace.EncryptionMetadata),
            Metadata = workspaceEncryptionSession.ToEncryptableMetadata(thumbnailMetadataJson)
        };

        var insertResult = await insertFileAttachmentQuery.Execute(
            workspace: workspace,
            parentFileExternalId: parentFileExternalId,
            uploader: uploader,
            attachment: attachment,
            cancellationToken: cancellationToken);

        if (insertResult == InsertFileAttachmentQuery.ResultCode.ParentFileNotFound)
            return new Result(Code: ResultCode.ParentNotFound);

        var encryptionMode = attachment.EncryptionMetadata.ToEncryptionMode(
            workspaceEncryptionSession: workspaceEncryptionSession,
            storageClient: workspace.Storage);

        var uploadDetails = new UploadFilePartDetails(
            FileKey: new FileKey
            {
                KeySecretPart = attachment.KeySecretPart,
                FileExternalId = attachment.ExternalId
            },
            MultipartUploadId: null,
            FileSizeInBytes: attachment.SizeInBytes,
            Part: FilePart.First((int)attachment.SizeInBytes),
            UploadAlgorithm: UploadAlgorithm.DirectUpload,
            EncryptionMode: encryptionMode);

        await workspace.UploadFilePart(
            input: PipeReader.Create(
                stream: thumbnailContent),
            uploadDetails: uploadDetails,
            cancellationToken: cancellationToken);

        // Storage upload succeeded — atomic switchover: mark new as completed + hard-delete
        // old thumbnails of same variant + enqueue storage cleanup jobs, all in one transaction.
        await finalizeThumbnailUploadQuery.Execute(
            workspace: workspace,
            newThumbnailExternalId: attachment.ExternalId,
            oldThumbnailFileIds: oldThumbnailFileIds,
            correlationId: correlationId,
            cancellationToken: cancellationToken);

        return new Result(
            Code: ResultCode.Ok,
            Attachment: attachment);
    }

    public record Result(
        ResultCode Code,
        InsertFileAttachmentQuery.AttachmentFile? Attachment = null);

    public enum ResultCode
    {
        Ok = 0,
        ParentNotFound,
        ParentNotThumbnailable
    }
}
