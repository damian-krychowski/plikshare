using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.AuditLog;
using PlikShare.Core.Authorization;
using PlikShare.Core.CorrelationId;
using PlikShare.Core.Encryption;
using PlikShare.Core.Protobuf;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.BulkDownload;
using PlikShare.Files.BulkDownload.Contracts;
using PlikShare.Files.Download;
using PlikShare.Files.Download.Contracts;
using PlikShare.Files.Id;
using PlikShare.Files.PreSignedLinks.Validation;
using PlikShare.Files.Preview.Comment;
using PlikShare.Files.Preview.Comment.CreateComment;
using PlikShare.Files.Preview.Comment.CreateComment.Contracts;
using PlikShare.Files.Preview.Comment.DeleteComment;
using PlikShare.Files.Preview.Comment.EditComment;
using PlikShare.Files.Preview.Comment.EditComment.Contracts;
using PlikShare.Files.Preview.GetDetails;
using PlikShare.Files.Preview.GetDetails.Contracts;
using PlikShare.Files.Preview.GetZipBulkDownloadLink;
using PlikShare.Files.Preview.GetZipBulkDownloadLink.Contracts;
using PlikShare.Files.Preview.GetZipContentDownloadLink;
using PlikShare.Files.Preview.GetZipContentDownloadLink.Contracts;
using PlikShare.Files.Preview.GetZipDetails;
using PlikShare.Files.Preview.GetZipDetails.Contracts;
using PlikShare.Files.Preview.SaveNote;
using PlikShare.Files.Preview.SaveNote.Contracts;
using PlikShare.Files.Records;
using PlikShare.Files.Rename;
using PlikShare.Files.Rename.Contracts;
using PlikShare.Files.UpdateSize;
using PlikShare.Files.Metadata;
using PlikShare.Files.Thumbnails;
using PlikShare.Files.Thumbnails.Generation;
using PlikShare.Files.UploadAttachment;
using PlikShare.Storages;
using PlikShare.Storages.Encryption.Authorization;
using PlikShare.Uploads.Algorithm;
using PlikShare.Workspaces.Validation;
using System.Globalization;
using System.IO.Pipelines;
using PlikShare.Workspaces.Cache;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Files;

public static class FilesEndpoints
{
    public static void MapFilesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/workspaces/{workspaceExternalId}/files")
            .WithTags("Files")
            .RequireAuthorization(policyNames: AuthPolicy.Internal)
            .AddEndpointFilter<ValidateWorkspaceFilter>()
            .AddEndpointFilter<ValidateWorkspaceEncryptionSessionFilter>();
        
        group.MapPost("/bulk-download-link", GetBulkDownloadLink)
            .WithName("GetBulkDownloadLink");

        group.MapPut("/{fileExternalId}/content", UpdateFileContent)
            .WithName("UpdateFileContent");

        group.MapPost("/{fileExternalId}/attachments", UploadFileAttachment)
            .WithName("UploadFileAttachment");

        group.MapPost("/{fileExternalId}/thumbnails", UploadFileThumbnail)
            .WithName("UploadFileThumbnail");

        group.MapDelete("/{fileExternalId}/thumbnails/{variant}", DeleteFileThumbnail)
            .WithName("DeleteFileThumbnail");

        group.MapPost("/{fileExternalId}/thumbnails/generate", GenerateFileThumbnails)
            .WithName("GenerateFileThumbnails");

        group.MapGet("/{fileExternalId}/download-converted", DownloadFileConverted)
            .WithName("DownloadFileConverted");

        group.MapGet("/{fileExternalId}/download-link", GetFileDownloadLink)
            .WithName("GetFileDownloadLink");

        group.MapPatch("/{fileExternalId}/name", UpdateFileName)
            .WithName("UpdateFileName");

        group.MapGet("/{fileExternalId}/preview/details", GetFilePreviewDetails)
            .WithName("GetFilePreviewDetails");
        
        group.MapPatch("/{fileExternalId}/note", UpdateNote)
            .WithName("UpdateFileNote");

        group.MapPost("/{fileExternalId}/comments", CreateComment)
            .WithName("CreateFileComment");

        group.MapDelete("/{fileExternalId}/comments/{commentExternalId}", DeleteComment)
            .WithName("DeleteFileComment");

        group.MapPatch("/{fileExternalId}/comments/{commentExternalId}", UpdateComment)
            .WithName("UpdateFileComment");

        group.MapGet("/{fileExternalId}/preview/zip", GetZipFilePreviewDetails)
            .WithName("GetZipFilePreviewDetails")
            .WithProtobufResponse();

        group.MapPost("/{fileExternalId}/preview/zip/download-link", GetZipContentDownloadLink)
            .WithName("GetZipContentDownloadLink");

        group.MapPost("/{fileExternalId}/preview/zip/bulk-download-link", GetZipBulkDownloadLink)
            .WithName("GetZipBulkDownloadLink");
    }


    private const int MaximumFileUploadPayloadSizeInBytes = Aes256GcmStreamingV1.MaximumPayloadSize;

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> UploadFileAttachment(
    [FromRoute] FileExtId fileExternalId,
    HttpContext httpContext,
    InsertFileAttachmentQuery insertFileAttachmentQuery,
    MarkFileAsUploadedQuery markFileAsUploadedQuery,
    AuditLogService auditLogService,
    CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        // Validate if the form is multipart
        if (!httpContext.Request.HasFormContentType)
            return HttpErrors.File.ExpectedMultipartFormDataContent();

        var form = await httpContext.Request.ReadFormAsync(cancellationToken);

        if (!form.Files.Any())
            return HttpErrors.File.MissingFile();
        
        var file = form.Files[0];
        var fileName = FileNames.TryGetNameAndExtension(file.FileName);

        if (fileName is null)
            return HttpErrors.File.MissingFileName();

        if (file.Length > MaximumFileUploadPayloadSizeInBytes)
            return HttpErrors.File.PayloadTooBig(
                file.Length);

        if (!form.TryGetValue("fileExternalId", out var externalIdValues) 
            || string.IsNullOrEmpty(externalIdValues) 
            || externalIdValues.Count != 1
            || !FileExtId.TryParse(externalIdValues[0], CultureInfo.InvariantCulture, out var attachmentFileExternalId))
        {
            return HttpErrors.File.MissingAttachmentFileExternalId();
        }

        var workspace = workspaceMembership.Workspace;
        var workspaceEncryptionSession = httpContext.TryGetWorkspaceEncryptionSession();

        var attachment = new InsertFileAttachmentQuery.AttachmentFile
        {
            ExternalId = attachmentFileExternalId,
            ContentType = workspaceEncryptionSession.ToEncryptableMetadata(file.ContentType),
            Name = workspaceEncryptionSession.ToEncryptableMetadata(fileName.Name),
            Extension = workspaceEncryptionSession.ToEncryptableMetadata(fileName.Extension),
            SizeInBytes = file.Length,
            KeySecretPart = workspace.Storage.GenerateFileKeySecretPart(),
            EncryptionMetadata = workspace.Storage.GenerateFileEncryptionMetadata(
                workspace.EncryptionMetadata)
        };

        var attachmentInsertionResult = await insertFileAttachmentQuery.Execute(
            workspace: workspace,
            parentFileExternalId: fileExternalId,
            uploader: new UserIdentity(
                UserExternalId: workspaceMembership.User.ExternalId),
            attachment: attachment,
            cancellationToken: cancellationToken);

        if (attachmentInsertionResult == InsertFileAttachmentQuery.ResultCode.ParentFileNotFound)
            return HttpErrors.File.NotFound(fileExternalId);

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
                stream: file.OpenReadStream()),
            uploadDetails: uploadDetails,
            cancellationToken: cancellationToken);

        await markFileAsUploadedQuery.Execute(
            fileExternalId: attachment.ExternalId,
            cancellationToken: cancellationToken);

        await auditLogService.LogWithFileContext(
            fileExternalId: fileExternalId,
            buildEntry: fileRef => Audit.File.AttachmentUploadedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                workspace: workspaceMembership.Workspace.ToAuditLogWorkspaceRef(),
                parentFile: fileRef,
                attachment: new Audit.FileRef
                {
                    ExternalId = attachment.ExternalId,
                    Name = workspaceEncryptionSession.Encode(fileName.Name),
                    Extension = workspaceEncryptionSession.Encode(fileName.Extension),
                    SizeInBytes = attachment.SizeInBytes
                }),
            cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> UploadFileThumbnail(
        [FromRoute] FileExtId fileExternalId,
        HttpContext httpContext,
        UploadFileThumbnailOperation uploadFileThumbnailOperation,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        if (!httpContext.Request.HasFormContentType)
            return HttpErrors.File.ExpectedMultipartFormDataContent();

        var form = await httpContext.Request.ReadFormAsync(cancellationToken);

        if (!form.Files.Any())
            return HttpErrors.File.MissingFile();

        var file = form.Files[0];
        var fileName = FileNames.TryGetNameAndExtension(file.FileName);

        if (fileName is null)
            return HttpErrors.File.MissingFileName();

        if (file.Length > MaximumFileUploadPayloadSizeInBytes)
            return HttpErrors.File.PayloadTooBig(file.Length);

        if (ContentTypeHelper.GetFileTypeFromExtension(fileName.Extension) != FileType.Image)
            return HttpErrors.File.ThumbnailMustBeImage();

        if (!form.TryGetValue("fileExternalId", out var externalIdValues)
            || string.IsNullOrEmpty(externalIdValues)
            || externalIdValues.Count != 1
            || !FileExtId.TryParse(
                externalIdValues[0],
                CultureInfo.InvariantCulture,
                out var thumbnailFileExternalId))
        {
            return HttpErrors.File.MissingAttachmentFileExternalId();
        }

        if (!form.TryGetValue("variant", out var variantValues)
            || string.IsNullOrEmpty(variantValues)
            || variantValues.Count != 1
            || !Enum.TryParse<ThumbnailVariant>(
                variantValues[0],
                ignoreCase: true,
                out var variant))
        {
            return HttpErrors.File.InvalidThumbnailVariant();
        }

        var workspaceEncryptionSession = httpContext.TryGetWorkspaceEncryptionSession();

        await using var fileStream = file.OpenReadStream();

        var result = await uploadFileThumbnailOperation.Execute(
            workspace: workspaceMembership.Workspace,
            parentFileExternalId: fileExternalId,
            thumbnailFileExternalId: thumbnailFileExternalId,
            variant: variant,
            thumbnailContent: fileStream,
            thumbnailSizeInBytes: file.Length,
            thumbnailContentType: file.ContentType,
            thumbnailFileName: fileName.Name,
            thumbnailFileExtension: fileName.Extension,
            uploader: new UserIdentity(
                UserExternalId: workspaceMembership.User.ExternalId),
            workspaceEncryptionSession: workspaceEncryptionSession,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        if (result.Code == UploadFileThumbnailOperation.ResultCode.ParentNotFound)
            return HttpErrors.File.NotFound(fileExternalId);

        if (result.Code == UploadFileThumbnailOperation.ResultCode.ParentNotThumbnailable)
            return HttpErrors.File.ParentNotThumbnailable();

        await auditLogService.LogWithFileContext(
            fileExternalId: fileExternalId,
            buildEntry: fileRef => Audit.File.AttachmentUploadedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                workspace: workspaceMembership.Workspace.ToAuditLogWorkspaceRef(),
                parentFile: fileRef,
                attachment: new Audit.FileRef
                {
                    ExternalId = result.Attachment!.ExternalId,
                    Name = workspaceEncryptionSession.Encode(fileName.Name),
                    Extension = workspaceEncryptionSession.Encode(fileName.Extension),
                    SizeInBytes = result.Attachment.SizeInBytes
                }),
            cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> DeleteFileThumbnail(
        [FromRoute] FileExtId fileExternalId,
        [FromRoute] string variant,
        HttpContext httpContext,
        DeleteFileThumbnailOperation deleteFileThumbnailOperation,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ThumbnailVariant>(
                variant,
                ignoreCase: true,
                out var parsedVariant))
        {
            return HttpErrors.File.InvalidThumbnailVariant();
        }

        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();
        var workspaceEncryptionSession = httpContext.TryGetWorkspaceEncryptionSession();

        var result = await deleteFileThumbnailOperation.Execute(
            workspace: workspaceMembership.Workspace,
            parentFileExternalId: fileExternalId,
            variant: parsedVariant,
            workspaceEncryptionSession: workspaceEncryptionSession,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        if (result.Code == DeleteFileThumbnailOperation.ResultCode.ParentNotFound)
            return HttpErrors.File.NotFound(fileExternalId);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>, JsonHttpResult<HttpError>>> GenerateFileThumbnails(
        [FromRoute] FileExtId fileExternalId,
        [FromBody] GenerateFileThumbnailsRequestDto request,
        HttpContext httpContext,
        GenerateFileThumbnailsOperation generateFileThumbnailsOperation,
        CancellationToken cancellationToken)
    {
        if (request.Variants is null || request.Variants.Count == 0)
            return HttpErrors.File.NoThumbnailVariantsRequested();

        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();
        var workspaceEncryptionSession = httpContext.TryGetWorkspaceEncryptionSession();

        var result = await generateFileThumbnailsOperation.Execute(
            workspace: workspaceMembership.Workspace,
            parentFileExternalId: fileExternalId,
            variants: request.Variants,
            triggeredByUserExternalId: workspaceMembership.User.ExternalId,
            workspaceEncryptionSession: workspaceEncryptionSession,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            GenerateFileThumbnailsOperation.ResultCode.Ok => TypedResults.Ok(),
            GenerateFileThumbnailsOperation.ResultCode.FfmpegUnavailable => HttpErrors.File.FfmpegUnavailable(),
            GenerateFileThumbnailsOperation.ResultCode.ParentNotFound => HttpErrors.File.NotFound(fileExternalId),
            GenerateFileThumbnailsOperation.ResultCode.ParentNotThumbnailable => HttpErrors.File.ParentNotThumbnailable(),
            GenerateFileThumbnailsOperation.ResultCode.NoVariants => HttpErrors.File.NoThumbnailVariantsRequested(),
            _ => HttpErrors.File.NotFound(fileExternalId)
        };
    }

    public class GenerateFileThumbnailsRequestDto
    {
        public required List<ThumbnailVariant> Variants { get; init; }
    }

    private static async Task<IResult> DownloadFileConverted(
        [FromRoute] FileExtId fileExternalId,
        [FromQuery] string format,
        HttpContext httpContext,
        DownloadFileConvertedOperation downloadFileConvertedOperation,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<DownloadImageFormat>(
                format,
                ignoreCase: true,
                out var targetFormat))
        {
            return HttpErrors.File.InvalidDownloadFormat();
        }

        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();
        var workspaceEncryptionSession = httpContext.TryGetWorkspaceEncryptionSession();

        var result = await downloadFileConvertedOperation.Execute(
            workspace: workspaceMembership.Workspace,
            parentFileExternalId: fileExternalId,
            targetFormat: targetFormat,
            workspaceEncryptionSession: workspaceEncryptionSession,
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            DownloadFileConvertedOperation.ResultCode.Ok => Results.File(
                fileContents: result.Content!,
                contentType: result.ContentType!,
                fileDownloadName: result.DownloadFileName!),
            DownloadFileConvertedOperation.ResultCode.FfmpegUnavailable => HttpErrors.File.FfmpegUnavailable(),
            DownloadFileConvertedOperation.ResultCode.ParentNotFound => HttpErrors.File.NotFound(fileExternalId),
            DownloadFileConvertedOperation.ResultCode.ParentNotThumbnailable => HttpErrors.File.ParentNotThumbnailable(),
            _ => HttpErrors.File.NotFound(fileExternalId)
        };
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> UpdateFileContent(
        [FromRoute] FileExtId fileExternalId,
        HttpContext httpContext,
        GetFilePreSignedDownloadLinkDetailsQuery getFilePreSignedDownloadLinkDetailsQuery,
        UpdateFileSizeQuery updateFileSizeQuery,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var file = getFilePreSignedDownloadLinkDetailsQuery.Execute(
            fileExternalId: fileExternalId,
            workspaceEncryptionSession: httpContext.TryGetWorkspaceEncryptionSession());

        if (file.Code == GetFilePreSignedDownloadLinkDetailsQuery.ResultCode.NotFound ||
            file.Details?.WorkspaceId != workspaceMembership.Workspace.Id)
            return HttpErrors.File.NotFound(
                fileExternalId);

        //for now only markdown files can be updated
        if (file.Details.Extension != ".md")
            return HttpErrors.File.WrongFileExtension(
                fileExternalId,
                ".md");

        var newSizeInBytes = (int)httpContext.Request.ContentLength!.Value;

        if (newSizeInBytes > MaximumFileUploadPayloadSizeInBytes)
            return HttpErrors.File.PayloadTooBig(
                newSizeInBytes);

        var encryptionMode = file.Details.EncryptionMetadata.ToEncryptionMode(
            workspaceEncryptionSession: httpContext.TryGetWorkspaceEncryptionSession(),
            storageClient: workspaceMembership.Workspace.Storage);

        var uploadDetails = new UploadFilePartDetails(
            FileKey: new FileKey
            {
                KeySecretPart = file.Details.KeySecretPart,
                FileExternalId = file.Details.ExternalId
            },
            MultipartUploadId: null,
            FileSizeInBytes: newSizeInBytes,
            Part: FilePart.First(newSizeInBytes),
            UploadAlgorithm: UploadAlgorithm.DirectUpload,
            EncryptionMode: encryptionMode);

        await workspaceMembership.Workspace.UploadFilePart(
            input: httpContext.Request.BodyReader,
            uploadDetails: uploadDetails,
            cancellationToken: cancellationToken);

        await updateFileSizeQuery.Execute(
            fileExternalId: file.Details.ExternalId,
            newSizeInBytes: newSizeInBytes,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        await auditLogService.LogWithFileContext(
            fileExternalId: fileExternalId,
            buildEntry: fileRef => Audit.File.ContentUpdatedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                workspace: workspaceMembership.Workspace.ToAuditLogWorkspaceRef(),
                file: fileRef),
            cancellationToken);

        return TypedResults.Ok();
    }

    private static Results<Ok<GetZipBulkDownloadLinkResponseDto>, NotFound<HttpError>, BadRequest<HttpError>> GetZipBulkDownloadLink(
        [FromRoute] FileExtId fileExternalId,
        [FromBody] GetZipBulkDownloadLinkRequestDto request,
        HttpContext httpContext,
        GetZipBulkDownloadLinkOperation getZipBulkDownloadLinkOperation)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var result = getZipBulkDownloadLinkOperation.Execute(
            workspace: workspaceMembership.Workspace,
            fileExternalId: fileExternalId,
            request: request,
            boxFolderId: null,
            boxLinkId: null,
            userIdentity: new UserIdentity(
                UserExternalId: workspaceMembership.User.ExternalId),
            workspaceEncryptionSession: httpContext.TryGetWorkspaceEncryptionSession());

        return result.Code switch
        {
            GetZipBulkDownloadLinkOperation.ResultCode.Ok => TypedResults.Ok(new GetZipBulkDownloadLinkResponseDto(
                DownloadPreSignedUrl: result.DownloadPreSignedUrl!)),

            GetZipBulkDownloadLinkOperation.ResultCode.FileNotFound =>
                HttpErrors.File.NotFound(fileExternalId),

            GetZipBulkDownloadLinkOperation.ResultCode.WrongFileExtension =>
                HttpErrors.File.WrongFileExtension(fileExternalId, ".zip"),

            GetZipBulkDownloadLinkOperation.ResultCode.EmptySelection =>
                HttpErrors.File.EmptyZipBulkSelection(fileExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(GetZipBulkDownloadLinkOperation),
                resultValueStr: result.Code.ToString())
        };
    }

    private static Results<Ok<GetZipContentDownloadLinkResponseDto>, NotFound<HttpError>, BadRequest<HttpError>> GetZipContentDownloadLink(
        [FromRoute] FileExtId fileExternalId,
        [FromBody] GetZipContentDownloadLinkRequestDto request,
        HttpContext httpContext,
        GetZipContentDownloadLinkOperation getZipContentDownloadLinkOperation,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var result = getZipContentDownloadLinkOperation.Execute(
            workspace: workspaceMembership.Workspace,
            fileExternalId: fileExternalId,
            zipFile: request.Item,
            contentDisposition: request.ContentDisposition,
            boxFolderId: null,
            boxLinkId: null,
            userIdentity: new UserIdentity(
                UserExternalId: workspaceMembership.User.ExternalId),
            workspaceEncryptionSession: httpContext.TryGetWorkspaceEncryptionSession(),
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            GetZipContentDownloadLinkOperation.ResultCode.Ok => TypedResults.Ok(new GetZipContentDownloadLinkResponseDto(
                DownloadPreSignedUrl: result.DownloadPreSignedUrl!)),

            GetZipContentDownloadLinkOperation.ResultCode.FileNotFound => 
                HttpErrors.File.NotFound(
                    fileExternalId),

            GetZipContentDownloadLinkOperation.ResultCode.WrongFileExtension => 
                HttpErrors.File.WrongFileExtension(
                    fileExternalId,
                    ".zip"),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(CreateFileCommentQuery),
                resultValueStr: result.Code.ToString())
        };
    }

    private static async ValueTask<Results<Ok<GetZipFileDetailsResponseDto>, NotFound<HttpError>, BadRequest<HttpError>>> GetZipFilePreviewDetails(
        [FromRoute] FileExtId fileExternalId,
        HttpContext httpContext,
        GetZipFileDetailsOperation getZipFileDetailsOperation,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var result = await getZipFileDetailsOperation.Execute(
            workspace: workspaceMembership.Workspace,
            fileExternalId: fileExternalId,
            boxFolderId: null,
            workspaceEncryptionSession: httpContext.TryGetWorkspaceEncryptionSession(),
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            GetZipFileDetailsOperation.ResultCode.Ok =>
                TypedResults.Ok(result.Response!),

            GetZipFileDetailsOperation.ResultCode.FileNotFound =>
                HttpErrors.File.NotFound(
                    fileExternalId),

            GetZipFileDetailsOperation.ResultCode.WrongFileExtension =>
                HttpErrors.File.WrongFileExtension(
                    fileExternalId,
                    ".zip"),

            GetZipFileDetailsOperation.ResultCode.ZipFileBroken =>
                HttpErrors.File.ZipFileBroken(
                    fileExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(GetZipFileDetailsOperation),
                resultValueStr: result.Code.ToString())
        };
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> UpdateComment(
        [FromBody] EditFileCommentRequestDto request,
        [FromRoute] FileExtId fileExternalId,
        [FromRoute] FileArtifactExtId commentExternalId,
        HttpContext httpContext,
        UpdateFileCommentQuery updateFileCommentQuery,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();
        var workspaceEncryptionSession = httpContext.TryGetWorkspaceEncryptionSession();

        var serializedContent = Json.Serialize(new CommentContentEntity(
            ContentJson: request.ContentJson,
            WasEdited: true));

        var resultCode = await updateFileCommentQuery.Execute(
            workspace: workspaceMembership.Workspace,
            fileExternalId: fileExternalId,
            commentExternalId: commentExternalId,
            userIdentity: new UserIdentity(
                UserExternalId: workspaceMembership.User.ExternalId),
            updatedCommentContent: workspaceEncryptionSession.ToEncryptableMetadata(serializedContent),
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateFileCommentQuery.ResultCode.Ok:
                await auditLogService.LogWithFileContext(
                    fileExternalId: fileExternalId,
                    buildEntry: fileRef => Audit.File.CommentEditedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspaceMembership.Workspace.ToAuditLogWorkspaceRef(),
                        file: fileRef,
                        commentExternalId: commentExternalId,
                        contentJson: workspaceEncryptionSession.Encode(request.ContentJson)),
                    cancellationToken);

                return TypedResults.Ok();

            case UpdateFileCommentQuery.ResultCode.FileNotFound:
                return HttpErrors.File.NotFound(fileExternalId);

            case UpdateFileCommentQuery.ResultCode.CommentNotFoundOrNotOwner:
                return HttpErrors.File.CommentNotFound(commentExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(CreateFileCommentQuery),
                    resultValueStr: resultCode.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> DeleteComment(
        [FromRoute] FileExtId fileExternalId,
        [FromRoute] FileArtifactExtId commentExternalId,
        HttpContext httpContext,
        DeleteFileCommentQuery deleteFileCommentQuery,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var resultCode = await deleteFileCommentQuery.Execute(
            workspace: workspaceMembership.Workspace,
            fileExternalId: fileExternalId,
            commentExternalId: commentExternalId,
            userIdentity: new UserIdentity(
                UserExternalId: workspaceMembership.User.ExternalId),
            isAdmin: workspaceMembership.User.HasAdminRole,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case DeleteFileCommentQuery.ResultCode.Ok:
                await auditLogService.LogWithFileContext(
                    fileExternalId: fileExternalId,
                    buildEntry: fileRef => Audit.File.CommentDeletedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspaceMembership.Workspace.ToAuditLogWorkspaceRef(),
                        file: fileRef,
                        commentExternalId: commentExternalId),
                    cancellationToken);

                return TypedResults.Ok();

            case DeleteFileCommentQuery.ResultCode.FileNotFound:
                return HttpErrors.File.NotFound(fileExternalId);

            case DeleteFileCommentQuery.ResultCode.CommentNotFoundOrNotOwner:
                return HttpErrors.File.CommentNotFound(commentExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(CreateFileCommentQuery),
                    resultValueStr: resultCode.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> CreateComment(
        [FromRoute] FileExtId fileExternalId,
        [FromBody] CreateFileCommentRequestDto request,
        HttpContext httpContext,
        CreateFileCommentQuery createFileCommentQuery,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();
        var workspaceEncryptionSession = httpContext.TryGetWorkspaceEncryptionSession();

        var serializedContent = Json.Serialize(new CommentContentEntity(
            ContentJson: request.ContentJson,
            WasEdited: false));

        var resultCode = await createFileCommentQuery.Execute(
            workspace: workspaceMembership.Workspace,
            fileExternalId: fileExternalId,
            userIdentity: new UserIdentity(
                UserExternalId: workspaceMembership.User.ExternalId),
            commentExternalId: request.ExternalId,
            commentContent: workspaceEncryptionSession.ToEncryptableMetadata(serializedContent),
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case CreateFileCommentQuery.ResultCode.Ok:
                await auditLogService.LogWithFileContext(
                    fileExternalId: fileExternalId,
                    buildEntry: fileRef => Audit.File.CommentCreatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspaceMembership.Workspace.ToAuditLogWorkspaceRef(),
                        file: fileRef,
                        commentExternalId: request.ExternalId,
                        contentJson: workspaceEncryptionSession.Encode(request.ContentJson)),
                    cancellationToken);

                return TypedResults.Ok();

            case CreateFileCommentQuery.ResultCode.FileNotFound:
                return HttpErrors.File.NotFound(fileExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(CreateFileCommentQuery),
                    resultValueStr: resultCode.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> UpdateNote(
        [FromRoute] FileExtId fileExternalId,
        [FromBody] SaveFileNoteRequestDto request,
        HttpContext httpContext,
        SaveFileNoteQuery saveFileNoteQuery,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var workspaceEncryptionSession = httpContext.TryGetWorkspaceEncryptionSession();

        var resultCode = await saveFileNoteQuery.Execute(
            workspace: workspaceMembership.Workspace,
            fileExternalId: fileExternalId,
            userIdentity: new UserIdentity(
                UserExternalId: workspaceMembership.User.ExternalId),
            content: request.ContentJson is null
                ? null
                : workspaceEncryptionSession.ToEncryptableMetadata(request.ContentJson),
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case SaveFileNoteQuery.ResultCode.Ok:
                await auditLogService.LogWithFileContext(
                    fileExternalId: fileExternalId,
                    buildEntry: fileRef => Audit.File.NoteSavedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspaceMembership.Workspace.ToAuditLogWorkspaceRef(),
                        file: fileRef),
                    cancellationToken);

                return TypedResults.Ok();

            case SaveFileNoteQuery.ResultCode.ContentNotChanged:
                return TypedResults.Ok();

            case SaveFileNoteQuery.ResultCode.FileNotFound:
                return HttpErrors.File.NotFound(fileExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(SaveFileNoteQuery),
                    resultValueStr: resultCode.ToString());
        }
    }

    private static Results<Ok<GetFilePreviewDetailsResponseDto>, NotFound<HttpError>> GetFilePreviewDetails(
        [FromRoute] FileExtId fileExternalId,
        [FromQuery] string[] fields,
        HttpContext httpContext,
        GetFilePreviewDetailsQuery getFilePreviewDetailsQuery)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var result = getFilePreviewDetailsQuery.Execute(
            workspace: workspaceMembership.Workspace,
            fileExternalId: fileExternalId,
            userIdentity: new UserIdentity(
                UserExternalId: workspaceMembership.User.ExternalId),
            requestedFields: fields
                .Select(EnumUtils.FromKebabCase<FilePreviewDetailsField>)
                .ToArray(),
            workspaceEncryptionSession: httpContext.TryGetWorkspaceEncryptionSession());

        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<GetBulkDownloadLinkResponseDto>, NotFound<HttpError>, BadRequest<HttpError>>>
        GetBulkDownloadLink(
            [FromBody] GetBulkDownloadLinkRequestDto request,
            HttpContext httpContext,
            GetBulkDownloadLinkOperation getBulkDownloadLinkOperation,
            AuditLogService auditLogService,
            CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var result = getBulkDownloadLinkOperation.Execute(
            workspace: workspaceMembership.Workspace,
            request: request,
            userIdentity: new UserIdentity(workspaceMembership.User.ExternalId),
            boxFolderId: null,
            boxLinkId: null,
            workspaceEncryptionSession: httpContext.TryGetWorkspaceEncryptionSession());

        switch (result.Code)
        {
            case GetBulkDownloadLinkOperation.ResultCode.Ok:
                await auditLogService.Log(
                    Audit.File.BulkDownloadLinkGeneratedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspaceMembership.Workspace.ToAuditLogWorkspaceRef(),
                        selectedFileExternalIds: request.SelectedFiles,
                        selectedFolderExternalIds: request.SelectedFolders),
                    cancellationToken);

                return TypedResults.Ok(new GetBulkDownloadLinkResponseDto
                {
                    PreSignedUrl = result.PreSignedUrl!
                });

            case GetBulkDownloadLinkOperation.ResultCode.FilesNotFound:
                return HttpErrors.File.SomeFilesNotFound(result.NotFoundFileExternalIds!);

            case GetBulkDownloadLinkOperation.ResultCode.FoldersNotFound:
                return HttpErrors.Folder.NotFound(result.NotFoundFolderExternalIds);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(GetFileDownloadLinkOperation),
                    resultValueStr: result.Code.ToString());
        }
    }

    private static async Task<Results<Ok<GetFileDownloadLinkResponseDto>, NotFound<HttpError>, BadRequest<HttpError>>>
        GetFileDownloadLink(
            [FromRoute] FileExtId fileExternalId,
            [FromQuery] string contentDisposition,
            HttpContext httpContext,
            GetFileDownloadLinkOperation getFileDownloadLinkOperation,
            AuditLogService auditLogService,
            CancellationToken cancellationToken)
    {
        if (!ContentDispositionHelper.TryParse(contentDisposition, out var contentDispositionType))
            return HttpErrors.File.InvalidContentDisposition(
                contentDisposition);

        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();
        
        var result = await getFileDownloadLinkOperation.Execute(
            workspace: workspaceMembership.Workspace,
            fileExternalId: fileExternalId,
            boxFolderId: null,
            boxLinkId: null,
            contentDisposition: contentDispositionType,
            userIdentity: new UserIdentity(workspaceMembership.User.ExternalId),
            enforceInternalPassThrough: false,
            workspaceEncryptionSession: httpContext.TryGetWorkspaceEncryptionSession(),
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case GetFileDownloadLinkOperation.ResultCode.Ok:
                await auditLogService.LogWithFileContext(
                    fileExternalId: fileExternalId,
                    buildEntry: fileRef => Audit.File.DownloadLinkGeneratedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspaceMembership.Workspace.ToAuditLogWorkspaceRef(),
                        file: fileRef),
                    cancellationToken);

                return TypedResults.Ok(
                    new GetFileDownloadLinkResponseDto(
                        DownloadPreSignedUrl: result.DownloadPreSignedUrl!));

            case GetFileDownloadLinkOperation.ResultCode.FileNotFound:
                return HttpErrors.File.NotFound(fileExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(GetFileDownloadLinkOperation),
                    resultValueStr: result.Code.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> UpdateFileName(
        [FromRoute] FileExtId fileExternalId,
        [FromBody] UpdateFileNameRequestDto request,
        HttpContext httpContext,
        UpdateFileNameQuery updateFileNameQuery,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var resultCode = await updateFileNameQuery.Execute(
            workspace: workspaceMembership.Workspace,
            fileExternalId: fileExternalId,
            name: httpContext
                .TryGetWorkspaceEncryptionSession()
                .ToEncryptableMetadata(request.Name),
            boxFolderId: null,
            userIdentity: new UserIdentity(workspaceMembership.User.ExternalId),
            isRenameAllowedByBoxPermissions: true,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateFileNameQuery.ResultCode.Ok:
                await auditLogService.LogWithFileContext(
                    fileExternalId: fileExternalId,
                    buildEntry: fileRef => Audit.File.RenamedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspaceMembership.Workspace.ToAuditLogWorkspaceRef(),
                        file: fileRef),
                    cancellationToken);

                return TypedResults.Ok();

            case UpdateFileNameQuery.ResultCode.FileNotFound:
                return HttpErrors.File.NotFound(fileExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateFileNameQuery),
                    resultValueStr: resultCode.ToString());
        }
    }
}