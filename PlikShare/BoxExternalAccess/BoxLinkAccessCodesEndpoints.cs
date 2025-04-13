using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.Antiforgery;
using PlikShare.Boxes.Permissions;
using PlikShare.BoxExternalAccess.Authorization;
using PlikShare.BoxExternalAccess.Contracts;
using PlikShare.BoxExternalAccess.Handler;
using PlikShare.BoxLinks.Validation;
using PlikShare.BulkDelete.Contracts;
using PlikShare.Core.Authorization;
using PlikShare.Core.Configuration;
using PlikShare.Core.CorrelationId;
using PlikShare.Core.CORS;
using PlikShare.Core.Protobuf;
using PlikShare.Core.Utils;
using PlikShare.Files.BulkDownload.Contracts;
using PlikShare.Files.Id;
using PlikShare.Files.Preview.GetZipContentDownloadLink.Contracts;
using PlikShare.Files.Preview.GetZipDetails.Contracts;
using PlikShare.Folders.Create.Contracts;
using PlikShare.Folders.Id;
using PlikShare.Folders.List.Contracts;
using PlikShare.Locks.CheckFileLocks;
using PlikShare.Locks.CheckFileLocks.Contracts;
using PlikShare.Uploads.GetDetails.Contracts;
using PlikShare.Uploads.Id;
using PlikShare.Uploads.Initiate.Contracts;
using PlikShare.Uploads.List.Contracts;
using PlikShare.Workspaces.CountSelectedItems.Contracts;
using PlikShare.Workspaces.SearchFilesTree.Contracts;

namespace PlikShare.BoxExternalAccess;

public static class BoxLinkAccessCodesEndpoints
{
    public static void MapBoxLinkAccessCodesEndpoints(this WebApplication app)
    {
        app.MapPost("/api/access-codes/start-session", StartSession)
            .WithTags("BoxLink_StartSession")
            .AllowAnonymous()
            .WithMetadata(new DisableAutoAntiforgeryCheck())
            .RequireCors(CorsPolicies.BoxLink);

        //this endpoint is duplication of the above but with additional access-code
        //in route to make it easier to handle CORS dynamically in box-widget scenarios
        app.MapPost("/api/access-codes/{accessCode}/start-session", StartSession)
            .WithTags("BoxLink_StartSessionWithAccessCode")
            .AllowAnonymous()
            .WithMetadata(new DisableAutoAntiforgeryCheck())
            .RequireCors(CorsPolicies.BoxLink);

        var group = app.MapGroup("/api/access-codes")
                .RequireAuthorization(policyNames: AuthPolicy.BoxLinkCookie)
                .WithTags("BoxLinkAccessCodes")
                .WithMetadata(new DisableAutoAntiforgeryCheck())
                .RequireCors(CorsPolicies.BoxLink);

        group.MapGet("/{accessCode}/html", GetBoxHtml)
            .WithName("BoxLink_GetBoxHtml")
            .AddEndpointFilter(new ValidateAccessCodeFilter());

        group.MapGet("/{accessCode}/{folderExternalId?}", GetBoxDetailsAndContent)
            .WithName("BoxLink_GetBoxDetailsAndContent")
            .AddEndpointFilter(new ValidateAccessCodeFilter())
            .WithProtobufResponse();

        group.MapGet("/{accessCode}/content/{folderExternalId?}", GetBoxContent)
            .WithName("BoxLink_GetBoxContent")
            .AddEndpointFilter(new ValidateAccessCodeFilter())
            .WithProtobufResponse();

        group.MapGet("/{accessCode}/files/{fileExternalId}/download-link", GetFileDownloadLink)
            .WithName("BoxLink_GetFileDownloadLink")
            .AddEndpointFilter(new ValidateAccessCodeFilter(
                BoxPermission.AllowList,
                BoxPermission.AllowDownload));

        group.MapPost("/{accessCode}/files/bulk-download-link", GetBulkDownloadLink)
            .WithName("BoxLink_GetBulkDownloadLink")
            .AddEndpointFilter(new ValidateAccessCodeFilter(
                BoxPermission.AllowList,
                BoxPermission.AllowDownload));

        group.MapPatch("/{accessCode}/files/{fileExternalId}/name", UpdateFileName)
            .WithName("BoxLink_UpdateFileName")
            .AddEndpointFilter(new ValidateAccessCodeFilter()); //access validation on query level

        group.MapPost("/{accessCode}/bulk-delete", DeleteFile)
            .WithName("BoxLink_DeleteFile")
            .AddEndpointFilter(new ValidateAccessCodeFilter());

        group.MapPost("/{accessCode}/folders", CreateFolder)
            .WithName("BoxLink_CreateFolder")
            .AddEndpointFilter(new ValidateAccessCodeFilter(
                BoxPermission.AllowList,
                BoxPermission.AllowCreateFolder));

        group.MapPost("/{accessCode}/folders/bulk", BulkCreateFolders)
            .WithName("BoxLink_BulkCreateFolder")
            .AddEndpointFilter(new ValidateAccessCodeFilter(
                BoxPermission.AllowList,
                BoxPermission.AllowCreateFolder))
            .WithProtobufResponse();

        group.MapPost("/{accessCode}/count-selected-items", CountSelectedItems)
            .WithName("BoxLink_CountSelectedItems")
            .AddEndpointFilter(new ValidateAccessCodeFilter(
                BoxPermission.AllowList));

        group.MapPost("/{accessCode}/search-files-tree", SearchFilesTree)
            .WithName("BoxLink_SearchFilesTree")
            .AddEndpointFilter(new ValidateAccessCodeFilter(
                BoxPermission.AllowList))
            .WithProtobufResponse();

        group.MapPatch("/{accessCode}/folders/{folderExternalId}/name", UpdateFolderName)
            .WithName("BoxLink_UpdateFolderName")
            .AddEndpointFilter(new ValidateAccessCodeFilter(
                BoxPermission.AllowList));

        group.MapGet("/{accessCode}/files/{fileExternalId}/preview/zip", GetZipFilePreviewDetails)
            .WithName("BoxLink_GetZipFilePreviewDetails")
            .AddEndpointFilter(new ValidateAccessCodeFilter(
                BoxPermission.AllowList,
                BoxPermission.AllowDownload))
            .WithProtobufResponse();

        group.MapPost("/{accessCode}/files/{fileExternalId}/preview/zip/download-link", GetZipContentDownloadLink)
            .WithName("BoxLink_GetZipContentDownloadLink")
            .AddEndpointFilter(new ValidateAccessCodeFilter(
                BoxPermission.AllowList,
                BoxPermission.AllowDownload));

        group.MapPatch("/{accessCode}/folders/move-items", MoveItemsToFolder)
            .WithName("BoxLink_MoveItemsToFolder")
            .AddEndpointFilter(new ValidateAccessCodeFilter(
                BoxPermission.AllowList,
                BoxPermission.AllowMoveItems));

        group.MapPost("/{accessCode}/uploads/initiate/bulk", BulkInitiateFileUpload)
            .WithName("BoxLink_BulkInitiateFileUpload")
            .AddEndpointFilter(new ValidateAccessCodeFilter(
                BoxPermission.AllowUpload))
            .WithProtobufResponse();

        group.MapGet("/{accessCode}/uploads/{fileUploadExternalId}", GetFileUploadDetails)
            .WithName("BoxLink_GetFileUploadDetails")
            .AddEndpointFilter(new ValidateAccessCodeFilter(
                BoxPermission.AllowUpload));

        group.MapPost("/{accessCode}/uploads/{fileUploadExternalId}/parts/{partNumber:int}/initiate", InitiateFilePartUpload)
            .WithName("BoxLink_InitiateFilePartUpload")
            .AddEndpointFilter(new ValidateAccessCodeFilter(
                BoxPermission.AllowUpload));

        group.MapPost("/{accessCode}/uploads/{fileUploadExternalId}/parts/{partNumber:int}/complete", CompleteFilePartUpload)
            .WithName("BoxLink_CompleteFilePartUpload")
            .AddEndpointFilter(new ValidateAccessCodeFilter(
                BoxPermission.AllowUpload));

        group.MapPost("/{accessCode}/uploads/{fileUploadExternalId}/complete", CompleteUpload)
            .WithName("BoxLink_CompleteUpload")
            .AddEndpointFilter(new ValidateAccessCodeFilter(
                BoxPermission.AllowUpload));

        group.MapGet("/{accessCode}/uploads", ListUploads)
            .WithName("BoxLink_ListUploads")
            .AddEndpointFilter(new ValidateAccessCodeFilter(
                BoxPermission.AllowUpload));
        
        group.MapPost("/{accessCode}/lock-status/files", CheckFileLocks)
            .WithName("BoxLink_CheckFileLocks");
    }

    private static CheckFileLocksResponseDto CheckFileLocks(
        [FromBody] CheckFileLocksRequestDto request,
        CheckFileLocksQuery checkFileLocksQuery)
    {
        var response = checkFileLocksQuery.Execute(
            request.ExternalIds);

        return response;
    }

    private static GetUploadsListResponseDto ListUploads(
        HttpContext httpContext,
        BoxExternalAccessHandler boxExternalAccessHandler)
    {
        return boxExternalAccessHandler.ListUploads(
            httpContext: httpContext,
            boxAccess: httpContext.GetBoxAccess());
    }

    private static ValueTask<Results<Ok<CompleteBoxFileUploadResponseDto>, BadRequest<HttpError>, NotFound<HttpError>>> CompleteUpload(
        [FromRoute] FileUploadExtId fileUploadExternalId,
        HttpContext httpContext,
        BoxExternalAccessHandler boxExternalAccessHandler,
        CancellationToken cancellationToken)
    {
        return boxExternalAccessHandler.CompleteUpload(
            fileUploadExternalId: fileUploadExternalId,
            boxAccess: httpContext.GetBoxAccess(),
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);
    }

    private static ValueTask<Results<Ok, NotFound<HttpError>>> CompleteFilePartUpload(
        [FromRoute] FileUploadExtId fileUploadExternalId,
        [FromRoute] int partNumber,
        [FromBody] CompleteBoxFilePartUploadRequestDto request,
        HttpContext httpContext,
        BoxExternalAccessHandler boxExternalAccessHandler,
        CancellationToken cancellationToken)
    {
        return boxExternalAccessHandler.CompleteFilePartUpload(
            fileUploadExternalId: fileUploadExternalId,
            partNumber: partNumber,
            request: request,
            boxAccess: httpContext.GetBoxAccess(),
            cancellationToken: cancellationToken);
    }

    private static Task<Results<Ok<InitiateBoxFilePartUploadResponseDto>, NotFound<HttpError>>> InitiateFilePartUpload(
        [FromRoute] FileUploadExtId fileUploadExternalId,
        [FromRoute] int partNumber,
        HttpContext httpContext,
        BoxExternalAccessHandler boxExternalAccessHandler,
        IConfig config,
        CancellationToken cancellationToken)
    {
        return boxExternalAccessHandler.InitiateFilePartUpload(
            fileUploadExternalId: fileUploadExternalId,
            partNumber: partNumber,
            boxAccess: httpContext.GetBoxAccess(),
            boxLinkId: httpContext.GetBoxLinkContext().Id,
            enforceInternalPassThrough: httpContext.Request.Headers.Origin != config.AppUrl,
            cancellationToken: cancellationToken);
    }

    private static Results<Ok<GetFileUploadDetailsResponseDto>, NotFound<HttpError>> GetFileUploadDetails(
        [FromRoute] FileUploadExtId fileUploadExternalId,
        HttpContext httpContext,
        BoxExternalAccessHandler boxExternalAccessHandler)
    {
        return boxExternalAccessHandler.GetFileUploadDetails(
            fileUploadExternalId: fileUploadExternalId,
            boxAccess: httpContext.GetBoxAccess());
    }

    private static Task<Results<Ok<BulkInitiateFileUploadResponseDto>, NotFound<HttpError>, StatusCodeHttpResult, BadRequest<HttpError>>> BulkInitiateFileUpload(
        HttpContext httpContext,
        BoxExternalAccessHandler boxExternalAccessHandler,
        CancellationToken cancellationToken)
    {
        var request = httpContext.GetProtobufRequest<BulkInitiateFileUploadRequestDto>();

        return boxExternalAccessHandler.BulkInitiateFileUpload(
            request: request,
            boxAccess: httpContext.GetBoxAccess(),
            boxLinkId: httpContext.GetBoxLinkContext().Id,
            cancellationToken: cancellationToken);
    }
    
    private static Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>, StatusCodeHttpResult>> MoveItemsToFolder(
        [FromBody] MoveBoxItemsToFolderRequestDto request,
        HttpContext httpContext,
        BoxExternalAccessHandler boxExternalAccessHandler,
        CancellationToken cancellationToken)
    {
        return boxExternalAccessHandler.MoveItemsToFolder(
            request: request,
            boxAccess: httpContext.GetBoxAccess(),
            cancellationToken: cancellationToken);
    }

    private static Task<Results<Ok, NotFound<HttpError>, StatusCodeHttpResult>> UpdateFolderName(
        [FromRoute] FolderExtId folderExternalId,
        [FromBody] UpdateBoxFolderNameRequestDto request,
        HttpContext httpContext,
        BoxExternalAccessHandler boxExternalAccessHandler,
        CancellationToken cancellationToken)
    {
        return boxExternalAccessHandler.UpdateFolderName(
            folderExternalId: folderExternalId,
            request: request,
            boxAccess: httpContext.GetBoxAccess(),
            cancellationToken: cancellationToken);
    }

    private static ValueTask<Results<Ok<GetZipFileDetailsResponseDto>, NotFound<HttpError>, BadRequest<HttpError>, JsonHttpResult<HttpError>, StatusCodeHttpResult>> GetZipFilePreviewDetails(
        [FromRoute] FileExtId fileExternalId,
        HttpContext httpContext,
        BoxExternalAccessHandler boxExternalAccessHandler,
        CancellationToken cancellationToken)
    {
        return boxExternalAccessHandler.GetZipFilePreviewDetails(
            httpContext: httpContext,
            fileExternalId: fileExternalId,
            boxAccess: httpContext.GetBoxAccess(),
            cancellationToken: cancellationToken);
    }

    private static Results<Ok<GetZipContentDownloadLinkResponseDto>, NotFound<HttpError>, BadRequest<HttpError>, StatusCodeHttpResult>
        GetZipContentDownloadLink(
            [FromRoute] FileExtId fileExternalId,
            [FromBody] GetZipContentDownloadLinkRequestDto request,
            HttpContext httpContext,
            BoxExternalAccessHandler boxExternalAccessHandler,
            CancellationToken cancellationToken)
    {
        return boxExternalAccessHandler.GetZipContentDownloadLink(
            fileExternalId: fileExternalId,
            request: request,
            boxAccess: httpContext.GetBoxAccess(),
            boxLinkId: httpContext.GetBoxLinkContext().Id,
            cancellationToken: cancellationToken);
    }

    private static Task<Results<Ok<BulkCreateFolderResponseDto>, BadRequest<HttpError>, NotFound<HttpError>, StatusCodeHttpResult>> BulkCreateFolders(
        HttpContext httpContext,
        BoxExternalAccessHandler boxExternalAccessHandler,
        CancellationToken cancellationToken)
    {
        var request = httpContext.GetProtobufRequest<BulkCreateFolderRequestDto>();

        return boxExternalAccessHandler.BulkCreateFolders(
            request: request,
            boxAccess: httpContext.GetBoxAccess(),
            cancellationToken: cancellationToken);
    }

    private static Results<Ok<CountSelectedItemsResponseDto>, StatusCodeHttpResult> CountSelectedItems(
        [FromBody] CountSelectedItemsRequestDto request,
        HttpContext httpContext,
        BoxExternalAccessHandler boxExternalAccessHandler)
    {
        return boxExternalAccessHandler.CountSelectedItems(
            request: request,
            boxAccess: httpContext.GetBoxAccess());
    }

    private static Results<Ok<SearchFilesTreeResponseDto>, StatusCodeHttpResult> SearchFilesTree(
        [FromBody] SearchFilesTreeRequestDto request,
        HttpContext httpContext,
        BoxExternalAccessHandler boxExternalAccessHandler)
    {
        return boxExternalAccessHandler.SearchFilesTree(
            request: request,
            boxAccess: httpContext.GetBoxAccess());
    }

    private static Task<Results<Ok<CreateFolderResponseDto>, NotFound<HttpError>, StatusCodeHttpResult>> CreateFolder(
        [FromBody] CreateFolderRequestDto request,
        HttpContext httpContext,
        BoxExternalAccessHandler boxExternalAccessHandler,
        CancellationToken cancellationToken)
    {
        return boxExternalAccessHandler.CreateFolder(
            request: request,
            boxAccess: httpContext.GetBoxAccess(),
            cancellationToken: cancellationToken);
    }

    private static Task<Results<Ok<BulkDeleteResponseDto>, StatusCodeHttpResult>> DeleteFile(
        [FromBody] BoxBulkDeleteRequestDto request,
        HttpContext httpContext,
        BoxExternalAccessHandler boxExternalAccessHandler,
        CancellationToken cancellationToken)
    {
        return boxExternalAccessHandler.BulkDelete(
            fileExternalIds: request.FileExternalIds.ToArray(),
            folderExternalIds: request.FolderExternalIds.ToArray(),
            fileUploadExternalIds: request.FileUploadExternalIds.ToArray(),
            boxAccess: httpContext.GetBoxAccess(),
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);
    }

    private static Task<Results<Ok, NotFound<HttpError>, StatusCodeHttpResult>> UpdateFileName(
        [FromRoute] FileExtId fileExternalId,
        [FromBody] UpdateBoxFileNameRequestDto request,
        HttpContext httpContext,
        BoxExternalAccessHandler boxExternalAccessHandler,
        CancellationToken cancellationToken)
    {
        return boxExternalAccessHandler.UpdateFileName(
            fileExternalId: fileExternalId,
            request: request,
            boxAccess: httpContext.GetBoxAccess(),
            cancellationToken: cancellationToken);
    }

    private static Results<Ok<GetBulkDownloadLinkResponseDto>, NotFound<HttpError>, BadRequest<HttpError>, StatusCodeHttpResult> GetBulkDownloadLink(
        [FromBody] GetBulkDownloadLinkRequestDto request,
        HttpContext httpContext,
        BoxExternalAccessHandler boxExternalAccessHandler)
    {
        return boxExternalAccessHandler.GetBulkDownloadLink(
            request: request,
            boxAccess: httpContext.GetBoxAccess(),
            boxLinkId: httpContext.GetBoxLinkContext().Id);
    }

    private static Task<Results<Ok<GetBoxFileDownloadLinkResponseDto>, NotFound<HttpError>, BadRequest<HttpError>, StatusCodeHttpResult>> GetFileDownloadLink(
        [FromRoute] FileExtId fileExternalId,
        [FromQuery] string contentDisposition,
        HttpContext httpContext,
        BoxExternalAccessHandler boxExternalAccessHandler,
        IConfig config,
        CancellationToken cancellationToken)
    {
        return boxExternalAccessHandler.GetFileDownloadLink(
            fileExternalId: fileExternalId,
            contentDisposition: contentDisposition,
            boxAccess: httpContext.GetBoxAccess(),
            boxLinkId: httpContext.GetBoxLinkContext().Id,
            enforceInternalPassThrough: httpContext.Request.Headers.Origin != config.AppUrl,
            cancellationToken: cancellationToken);
    }

    private static Results<Ok<GetFolderContentResponseDto>, NotFound<HttpError>> GetBoxContent(
        [FromRoute] FolderExtId? folderExternalId,
        HttpContext httpContext,
        BoxExternalAccessHandler boxExternalAccessHandler)
    {
        return boxExternalAccessHandler.GetBoxContent(
            boxAccess: httpContext.GetBoxAccess(),
            folderExternalId: folderExternalId,
            httpContext: httpContext);
    }

    private static Results<Ok<GetBoxDetailsAndContentResponseDto>, NotFound<HttpError>> GetBoxDetailsAndContent(
        [FromRoute] FolderExtId? folderExternalId,
        HttpContext httpContext,
        BoxExternalAccessHandler boxExternalAccessHandler)
    {
        return boxExternalAccessHandler.GetBoxDetailsContent(
            httpContext: httpContext,
            boxAccess: httpContext.GetBoxAccess(),
            folderExternalId: folderExternalId);
    }

    private static Results<Ok<GetBoxHtmlResponseDto>, NotFound<HttpError>> GetBoxHtml(
        HttpContext httpContext,
        BoxExternalAccessHandler boxExternalAccessHandler)
    {
        return boxExternalAccessHandler.GetBoxHtml(
            boxAccess: httpContext.GetBoxAccess());
    }

    private static IResult StartSession(
        HttpContext httpContext,
        BoxLinkTokenService boxLinkTokenService,
        CancellationToken cancellationToken)
    {
        var token = boxLinkTokenService.Generate();
        
        httpContext.Response.Headers.Append(HeaderName.BoxLinkToken, token);
        httpContext.Response.Headers.Append("Access-Control-Expose-Headers", HeaderName.BoxLinkToken);

        return Results.Ok();
    }
}

