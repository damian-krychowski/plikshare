using PlikShare.Core.Encryption;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Files.Metadata;
using PlikShare.Files.UploadAttachment;
using PlikShare.Workspaces.Cache;

namespace PlikShare.MediaProcessing;

public class UploadFileThumbnailOperation(
    GetThumbnailsQuery getThumbnailsQuery,
    ValidateThumbnailParentQuery validateThumbnailParentQuery,
    InsertAndFinalizeThumbnailQuery insertAndFinalizeThumbnailQuery)
{
    public async Task<Result> Execute(
        WorkspaceContext workspace,
        FileExtId parentFileExternalId,
        ThumbnailDescriptor thumbnail,
        Func<Stream> getContent,
        IUserIdentity uploader,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var validation = validateThumbnailParentQuery.Execute(
            workspace: workspace,
            parentFileExternalId: parentFileExternalId,
            workspaceEncryptionSession: workspaceEncryptionSession);

        if (validation == ValidateThumbnailParentQuery.ResultCode.NotFound)
            return new Result(Code: ResultCode.ParentNotFound);

        if (validation == ValidateThumbnailParentQuery.ResultCode.NotThumbnailable)
            return new Result(Code: ResultCode.ParentNotThumbnailable);
        
        var existingThumbnails = getThumbnailsQuery.Execute(
            workspace: workspace,
            parentFileExternalId: parentFileExternalId,
            workspaceEncryptionSession: workspaceEncryptionSession);

        var oldThumbnailFileIds = existingThumbnails
            .Where(t => t.Variant == thumbnail.Variant)
            .Select(t => t.Id)
            .ToList();

        var encryptionMetadata = workspace.GenerateFileEncryptionMetadata();

        await using var fileStream = getContent();

        var etag = await thumbnail.UploadAndHash(
            workspace: workspace,
            content: fileStream,
            encryptionMode: workspace.GetFileEncryptionMode(
                fileEncryptionMetadata: encryptionMetadata,
                workspaceEncryptionSession: workspaceEncryptionSession),
            cancellationToken: cancellationToken);

        var attachment = new InsertFileAttachmentQuery.AttachmentFile
        {
            ExternalId = thumbnail.FileKey.FileExternalId,
            KeySecretPart = thumbnail.FileKey.KeySecretPart,
            SizeInBytes = thumbnail.SizeInBytes,
            EncryptionMetadata = encryptionMetadata,

            ContentType = workspace.ToEncryptableMetadata(
                thumbnail.ContentType,
                workspaceEncryptionSession),

            Name = workspace.ToEncryptableMetadata(
                thumbnail.FileName,
                workspaceEncryptionSession),

            Extension = workspace.ToEncryptableMetadata(
                thumbnail.FileExtension,
                workspaceEncryptionSession),

            Metadata = workspace.ToEncryptableMetadata(
                value: Json.Serialize<FileMetadata>(
                    new ThumbnailFileMetadata
                    {
                        Variant = thumbnail.Variant,
                        Etag = etag
                    }),
                workspaceEncryptionSession: workspaceEncryptionSession)
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
        ParentNotFound,
        ParentNotThumbnailable
    }
}
