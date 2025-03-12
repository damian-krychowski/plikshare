using System.Globalization;
using System.IO.Pipelines;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
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
using PlikShare.Files.Preview.Comment.CreateComment;
using PlikShare.Files.Preview.Comment.CreateComment.Contracts;
using PlikShare.Files.Preview.Comment.DeleteComment;
using PlikShare.Files.Preview.Comment.EditComment;
using PlikShare.Files.Preview.Comment.EditComment.Contracts;
using PlikShare.Files.Preview.GetDetails;
using PlikShare.Files.Preview.GetDetails.Contracts;
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
using PlikShare.Files.UploadAttachment;
using PlikShare.Storages;
using PlikShare.Storages.FileReading;
using PlikShare.Uploads.Algorithm;
using PlikShare.Uploads.Cache;
using PlikShare.Workspaces.Validation;

namespace PlikShare.Files;

public static class FilesEndpoints
{
    public static void MapFilesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/workspaces/{workspaceExternalId}/files")
            .WithTags("Files")
            .RequireAuthorization(policyNames: AuthPolicy.Internal)
            .AddEndpointFilter<ValidateWorkspaceFilter>();
        
        group.MapPost("/bulk-download-link", GetBulkDownloadLink)
            .WithName("GetBulkDownloadLink");

        group.MapPut("/{fileExternalId}/content", UpdateFileContent)
            .WithName("UpdateFileContent");

        group.MapPost("/{fileExternalId}/attachments", UploadFileAttachment)
            .WithName("UploadFileAttachment");

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
    }


    private const int MaximumFileUploadPayloadSizeInBytes = Aes256GcmStreaming.MaximumPayloadSize;

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> UploadFileAttachment(
    [FromRoute] FileExtId fileExternalId,
    HttpContext httpContext,
    InsertFileAttachmentQuery insertFileAttachmentQuery,
    MarkFileAsUploadedQuery markFileAsUploadedQuery,
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
        
        var attachment = new InsertFileAttachmentQuery.AttachmentFile
        {
            ExternalId = attachmentFileExternalId,
            ContentType = file.ContentType,
            Name = fileName.Name,
            Extension = fileName.Extension,
            SizeInBytes = file.Length,
            S3KeySecretPart = workspace.Storage.GenerateFileS3KeySecretPart(),
            Encryption = workspace.Storage.GenerateFileEncryptionDetails()
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
        
        //todo handle failures
        await FileWriter.Write(
            file: new FileToUploadDetails
            {
                SizeInBytes = attachment.SizeInBytes,
                Encryption = attachment.Encryption,
                S3FileKey = new S3FileKey
                {
                    S3KeySecretPart = attachment.S3KeySecretPart,
                    FileExternalId = attachment.ExternalId
                },
                S3UploadId = string.Empty,
            },
            part: FilePartDetails.First(
                sizeInBytes: (int) attachment.SizeInBytes, //the cast is ok because attachment imported here has a size limit
                uploadAlgorithm: UploadAlgorithm.DirectUpload),
            workspace: workspaceMembership.Workspace,
            input: PipeReader.Create(
                stream: file.OpenReadStream()), 
            cancellationToken: cancellationToken);

        await markFileAsUploadedQuery.Execute(
            fileExternalId: attachment.ExternalId,
            cancellationToken: cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> UpdateFileContent(
        [FromRoute] FileExtId fileExternalId,
        HttpContext httpContext,
        GetFilePreSignedDownloadLinkDetailsQuery getFilePreSignedDownloadLinkDetailsQuery,
        UpdateFileSizeQuery updateFileSizeQuery,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var file = getFilePreSignedDownloadLinkDetailsQuery.Execute(
            fileExternalId: fileExternalId);

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

        await FileWriter.Write(
            file: new FileToUploadDetails
            {
                SizeInBytes = newSizeInBytes,
                Encryption = file.Details.Encryption,
                S3FileKey = new S3FileKey
                {
                    S3KeySecretPart = file.Details.S3KeySecretPart,
                    FileExternalId = file.Details.ExternalId
                },
                S3UploadId = string.Empty,
            },
            part: FilePartDetails.First(
                sizeInBytes: newSizeInBytes,
                uploadAlgorithm: UploadAlgorithm.DirectUpload),
            workspace: workspaceMembership.Workspace,
            input: httpContext.Request.BodyReader,
            cancellationToken: cancellationToken);

        await updateFileSizeQuery.Execute(
            fileExternalId: file.Details.ExternalId,
            newSizeInBytes: newSizeInBytes,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        return TypedResults.Ok();
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
            userIdentity: new UserIdentity(
                UserExternalId: workspaceMembership.User.ExternalId),
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
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            GetZipFileDetailsOperation.ResultCode.Ok => TypedResults.Ok(new GetZipFileDetailsResponseDto
            {
                Items = result
                    .Entries!
                    .Where(e => e.UncompressedSize > 0)
                    .Select(e => new GetZipFileDetailsItemDto
                    {
                        FilePath = e.FileName,
                        CompressedSizeInBytes = e.CompressedSize,
                        SizeInBytes = e.UncompressedSize,
                        CompressionMethod = e.CompressionMethod,
                        FileNameLength = e.FileNameLength,
                        IndexInArchive = e.IndexInArchive,
                        OffsetToLocalFileHeader = e.OffsetToLocalFileHeader
                    })
                    .ToList()
            }),

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
                operationName: nameof(CreateFileCommentQuery),
                resultValueStr: result.Code.ToString())
        };
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> UpdateComment(
        [FromBody] EditFileCommentRequestDto request,
        [FromRoute] FileExtId fileExternalId,
        [FromRoute] FileArtifactExtId commentExternalId,
        HttpContext httpContext,
        UpdateFileCommentQuery updateFileCommentQuery,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var resultCode = await updateFileCommentQuery.Execute(
            workspace: workspaceMembership.Workspace,
            fileExternalId: fileExternalId,
            commentExternalId: commentExternalId,
            userIdentity: new UserIdentity(
                UserExternalId: workspaceMembership.User.ExternalId),
            updatedCommentContentJson: request.ContentJson,
            cancellationToken: cancellationToken);

        return resultCode switch
        {
            UpdateFileCommentQuery.ResultCode.Ok => TypedResults.Ok(),

            UpdateFileCommentQuery.ResultCode.FileNotFound =>
                HttpErrors.File.NotFound(
                    fileExternalId),

            UpdateFileCommentQuery.ResultCode.CommentNotFoundOrNotOwner => 
                HttpErrors.File.CommentNotFound(
                    commentExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(CreateFileCommentQuery),
                resultValueStr: resultCode.ToString())
        };
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> DeleteComment(
        [FromRoute] FileExtId fileExternalId,
        [FromRoute] FileArtifactExtId commentExternalId,
        HttpContext httpContext,
        DeleteFileCommentQuery deleteFileCommentQuery,
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

        return resultCode switch
        {
            DeleteFileCommentQuery.ResultCode.Ok => TypedResults.Ok(),

            DeleteFileCommentQuery.ResultCode.FileNotFound => 
                HttpErrors.File.NotFound(
                    fileExternalId),

            DeleteFileCommentQuery.ResultCode.CommentNotFoundOrNotOwner => 
                HttpErrors.File.CommentNotFound(
                    commentExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(CreateFileCommentQuery),
                resultValueStr: resultCode.ToString())
        };
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> CreateComment(
        [FromRoute] FileExtId fileExternalId,
        [FromBody] CreateFileCommentRequestDto request,
        HttpContext httpContext,
        CreateFileCommentQuery createFileCommentQuery,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var resultCode = await createFileCommentQuery.Execute(
            workspace: workspaceMembership.Workspace,
            fileExternalId: fileExternalId,
            userIdentity: new UserIdentity(
                UserExternalId: workspaceMembership.User.ExternalId),
            commentExternalId: request.ExternalId,
            commentContentJson: request.ContentJson,
            cancellationToken: cancellationToken);

        return resultCode switch
        {
            CreateFileCommentQuery.ResultCode.Ok => TypedResults.Ok(),

            CreateFileCommentQuery.ResultCode.FileNotFound => 
                HttpErrors.File.NotFound(
                    fileExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(CreateFileCommentQuery),
                resultValueStr: resultCode.ToString())
        };
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> UpdateNote(
        [FromRoute] FileExtId fileExternalId,
        [FromBody] SaveFileNoteRequestDto request,
        HttpContext httpContext,
        SaveFileNoteQuery saveFileNoteQuery,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var resultCode = await saveFileNoteQuery.Execute(
            workspace: workspaceMembership.Workspace,
            fileExternalId: fileExternalId,
            userIdentity: new UserIdentity(
                UserExternalId: workspaceMembership.User.ExternalId),
            contentJson: request.ContentJson,
            cancellationToken: cancellationToken);
        
        return resultCode switch
        {
            SaveFileNoteQuery.ResultCode.Ok => TypedResults.Ok(),

            SaveFileNoteQuery.ResultCode.FileNotFound => 
                HttpErrors.File.NotFound(
                    fileExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(SaveFileNoteQuery),
                resultValueStr: resultCode.ToString())
        };
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
                .ToArray());

        return TypedResults.Ok(result);
    }

    private static Results<Ok<GetBulkDownloadLinkResponseDto>, NotFound<HttpError>, BadRequest<HttpError>>
        GetBulkDownloadLink(
            [FromBody] GetBulkDownloadLinkRequestDto request,
            HttpContext httpContext,
            GetBulkDownloadLinkOperation getBulkDownloadLinkOperation)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var result = getBulkDownloadLinkOperation.Execute(
            workspace: workspaceMembership.Workspace,
            request: request,
            userIdentity: new UserIdentity(workspaceMembership.User.ExternalId),
            boxFolderId: null);

        return result.Code switch
        {
            GetBulkDownloadLinkOperation.ResultCode.Ok => TypedResults.Ok(new GetBulkDownloadLinkResponseDto{
                    PreSignedUrl = result.PreSignedUrl!
            }),

            GetBulkDownloadLinkOperation.ResultCode.FilesNotFound => 
                HttpErrors.File.SomeFilesNotFound(
                    result.NotFoundFileExternalIds!),

            GetBulkDownloadLinkOperation.ResultCode.FoldersNotFound => 
                HttpErrors.Folder.NotFound(
                    result.NotFoundFolderExternalIds),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(GetFileDownloadLinkOperation),
                resultValueStr: result.Code.ToString())
        };
    }

    private static async Task<Results<Ok<GetFileDownloadLinkResponseDto>, NotFound<HttpError>, BadRequest<HttpError>>>
        GetFileDownloadLink(
            [FromRoute] FileExtId fileExternalId,
            [FromQuery] string contentDisposition,
            HttpContext httpContext,
            GetFileDownloadLinkOperation getFileDownloadLinkOperation,
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
            contentDisposition: contentDispositionType,
            userIdentity: new UserIdentity(workspaceMembership.User.ExternalId),
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            GetFileDownloadLinkOperation.ResultCode.Ok => TypedResults.Ok(
                new GetFileDownloadLinkResponseDto(
                    DownloadPreSignedUrl: result.DownloadPreSignedUrl!)),

            GetFileDownloadLinkOperation.ResultCode.FileNotFound => 
                HttpErrors.File.NotFound(
                    fileExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(GetFileDownloadLinkOperation),
                resultValueStr: result.Code.ToString())
        };
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> UpdateFileName(
        [FromRoute] FileExtId fileExternalId,
        [FromBody] UpdateFileNameRequestDto request,
        HttpContext httpContext,
        UpdateFileNameQuery updateFileNameQuery,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var resultCode = await updateFileNameQuery.Execute(
            workspace: workspaceMembership.Workspace,
            fileExternalId: fileExternalId,
            name: request.Name,
            boxFolderId: null,
            userIdentity: new UserIdentity(workspaceMembership.User.ExternalId),
            isRenameAllowedByBoxPermissions: true,
            cancellationToken: cancellationToken);

        return resultCode switch
        {
            UpdateFileNameQuery.ResultCode.Ok => 
                TypedResults.Ok(),

            UpdateFileNameQuery.ResultCode.FileNotFound => 
                HttpErrors.File.NotFound(
                    fileExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(UpdateFileNameQuery),
                resultValueStr: resultCode.ToString())
        };
    }
}