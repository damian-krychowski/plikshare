using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Id;
using PlikShare.Boxes.Permissions;
using PlikShare.BoxExternalAccess.Authorization;
using PlikShare.BoxExternalAccess.Contracts;
using PlikShare.BoxExternalAccess.Handler;
using PlikShare.BoxExternalAccess.Invitations.Accept;
using PlikShare.BoxExternalAccess.Invitations.Reject;
using PlikShare.BoxExternalAccess.LeaveBox;
using PlikShare.Core.Authorization;
using PlikShare.Core.CorrelationId;
using PlikShare.Core.Protobuf;
using PlikShare.Core.Utils;
using PlikShare.Files.BulkDownload.Contracts;
using PlikShare.Files.Id;
using PlikShare.Files.Preview.GetZipContentDownloadLink.Contracts;
using PlikShare.Files.Preview.GetZipDetails.Contracts;
using PlikShare.Folders.Create.Contracts;
using PlikShare.Folders.Id;
using PlikShare.Folders.List.Contracts;
using PlikShare.Uploads.GetDetails.Contracts;
using PlikShare.Uploads.Id;
using PlikShare.Uploads.Initiate.Contracts;
using PlikShare.Uploads.List.Contracts;
using PlikShare.Users.Middleware;
using PlikShare.Workspaces.CountSelectedItems.Contracts;
using PlikShare.Workspaces.SearchFilesTree.Contracts;

namespace PlikShare.BoxExternalAccess;

public static class BoxExternalAccessEndpoints
{
    public static void MapBoxExternalAccessEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/boxes")
            .WithTags("BoxExternalAccess")
            .RequireAuthorization(policyNames: AuthPolicy.Internal);

        group.MapPost("/{boxExternalId}/accept-invitation", AcceptBoxInvitation)
            .WithName("BoxExternalAccess_AcceptBoxInvitation");

        group.MapPost("/{boxExternalId}/reject-invitation", RejectBoxInvitation)
            .WithName("BoxExternalAccess_RejectBoxInvitation");

        group.MapDelete("/{boxExternalId}", LeaveBoxMembership)
            .WithName("BoxExternalAccess_LeaveBoxMembership")
            .AddEndpointFilter(new ValidateExternalBoxFilter());
        
        group.MapGet("/{boxExternalId}/html", GetBoxHtml)
            .WithName("BoxExternalAccess_GetBoxHtml")
            .AddEndpointFilter(new ValidateExternalBoxFilter());

        group.MapGet("/{boxExternalId}/{folderExternalId?}", GetBoxDetailsAndContent)
            .WithName("BoxExternalAccess_GetBoxDetailsAndContent")
            .AddEndpointFilter(new ValidateExternalBoxFilter())
            .WithProtobufResponse();

        group.MapGet("/{boxExternalId}/content/{folderExternalId?}", GetBoxContent)
            .WithName("BoxExternalAccess_GetBoxContent")
            .AddEndpointFilter(new ValidateExternalBoxFilter())
            .WithProtobufResponse();

        group.MapGet("/{boxExternalId}/files/{fileExternalId}/download-link", GetFileDownloadLink)
            .WithName("BoxExternalAccess_GetFileDownloadLink")
            .AddEndpointFilter(new ValidateExternalBoxFilter(
                BoxPermission.AllowList,
                BoxPermission.AllowDownload));

        group.MapPost("/{boxExternalId}/files/bulk-download-link", GetBulkDownloadLink)
            .WithName("BoxExternalAccess_GetBulkDownloadLink")
            .AddEndpointFilter(new ValidateExternalBoxFilter(
                BoxPermission.AllowList,
                BoxPermission.AllowDownload));

        group.MapPatch("/{boxExternalId}/files/{fileExternalId}/name", UpdateFileName)
            .WithName("BoxExternalAccess_UpdateFileName")
            .AddEndpointFilter(new ValidateExternalBoxFilter()); //access validation on query level

        group.MapGet("/{boxExternalId}/files/{fileExternalId}/preview/zip", GetZipFilePreviewDetails)
            .WithName("BoxExternalAccess_GetZipFilePreviewDetails")
            .AddEndpointFilter(new ValidateExternalBoxFilter(
                BoxPermission.AllowList,
                BoxPermission.AllowDownload))
            .WithProtobufResponse();

        group.MapPost("/{boxExternalId}/files/{fileExternalId}/preview/zip/download-link", GetZipContentDownloadLink)
            .WithName("BoxExternalAccess_GetZipContentDownloadLink")
            .AddEndpointFilter(new ValidateExternalBoxFilter(
                BoxPermission.AllowList,
                BoxPermission.AllowDownload));

        group.MapPost("/{boxExternalId}/bulk-delete", DeleteFile)
            .WithName("BoxExternalAccess_DeleteFile")
            .AddEndpointFilter(new ValidateExternalBoxFilter()); //checks permission inside method

        group.MapPost("/{boxExternalId}/folders", CreateFolder)
            .WithName("BoxExternalAccess_CreateFolder")
            .AddEndpointFilter(new ValidateExternalBoxFilter(
                BoxPermission.AllowList,
                BoxPermission.AllowCreateFolder));

        group.MapPost("/{boxExternalId}/folders/bulk", BulkCreateFolders)
            .WithName("BoxExternalAccess_BulkCreateFolder")
            .AddEndpointFilter(new ValidateExternalBoxFilter(
                BoxPermission.AllowList,
                BoxPermission.AllowCreateFolder))
            .WithProtobufResponse();

        group.MapPost("/{boxExternalId}/count-selected-items", CountSelectedItems)
            .WithName("BoxExternalAccess_CountSelectedItems")
            .AddEndpointFilter(new ValidateExternalBoxFilter(
                BoxPermission.AllowList));

        group.MapPost("/{boxExternalId}/search-files-tree", SearchFilesTree)
            .WithName("BoxExternalAccess_SearchFilesTree")
            .AddEndpointFilter(new ValidateExternalBoxFilter(
                BoxPermission.AllowList))
            .WithProtobufResponse();

        group.MapPatch("/{boxExternalId}/folders/{folderExternalId}/name", UpdateFolderName)
            .WithName("BoxExternalAccess_UpdateFolderName")
            .AddEndpointFilter(new ValidateExternalBoxFilter(
                BoxPermission.AllowList,
                BoxPermission.AllowRenameFolder));

        group.MapPatch("/{boxExternalId}/folders/move-items", MoveItemsToFolder)
            .WithName("BoxExternalAccess_MoveItemsToFolder")
            .AddEndpointFilter(new ValidateExternalBoxFilter(
                BoxPermission.AllowList,
                BoxPermission.AllowMoveItems));

        group.MapPost("/{boxExternalId}/uploads/initiate/bulk", BulkInitiateFileUpload)
            .WithName("BoxExternalAccess_BulkInitiateFileUpload")
            .AddEndpointFilter(new ValidateExternalBoxFilter(
                BoxPermission.AllowUpload))
            .WithProtobufResponse();

        group.MapGet("/{boxExternalId}/uploads/{fileUploadExternalId}", GetFileUploadDetails)
            .WithName("BoxExternalAccess_GetFileUploadDetails")
            .AddEndpointFilter(new ValidateExternalBoxFilter(
                BoxPermission.AllowUpload));

        group.MapPost("/{boxExternalId}/uploads/{fileUploadExternalId}/parts/{partNumber:int}/initiate", InitiateFilePartUpload)
            .WithName("BoxExternalAccess_InitiateFilePartUpload")
            .AddEndpointFilter(new ValidateExternalBoxFilter(
                BoxPermission.AllowUpload));

        group.MapPost("/{boxExternalId}/uploads/{fileUploadExternalId}/parts/{partNumber:int}/complete", CompleteFilePartUpload)
            .WithName("BoxExternalAccess_CompleteFilePartUpload")
            .AddEndpointFilter(new ValidateExternalBoxFilter(
                BoxPermission.AllowUpload));

        group.MapPost("/{boxExternalId}/uploads/{fileUploadExternalId}/complete", CompleteUpload)
            .WithName("BoxExternalAccess_CompleteUpload")
            .AddEndpointFilter(new ValidateExternalBoxFilter(
                BoxPermission.AllowUpload));

        group.MapGet("/{boxExternalId}/uploads", ListUploads)
            .WithName("BoxExternalAccess_ListUploads")
            .AddEndpointFilter(new ValidateExternalBoxFilter(
                BoxPermission.AllowUpload));
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> LeaveBoxMembership(
        [FromRoute] BoxExtId boxExternalId,
        HttpContext httpContext,
        BoxMembershipCache boxMembershipCache,
        LeaveBoxMembershipQuery leaveBoxMembershipQuery,
        CancellationToken cancellationToken)
    {
        var user = httpContext.GetUserContext();

        var boxMembership = await boxMembershipCache.TryGetBoxMembership(
            boxExternalId,
            user.Id,
            cancellationToken);

        if (boxMembership is null)
            return HttpErrors.Box.MemberNotFound(boxExternalId, user.ExternalId);

        var result = await leaveBoxMembershipQuery.Execute(
            boxMembership: boxMembership,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        if (result == LeaveBoxMembershipQuery.ResultCode.Ok)
        {
            await boxMembershipCache.InvalidateEntry(boxMembership, cancellationToken);
            return TypedResults.Ok();
        }

        if (result == LeaveBoxMembershipQuery.ResultCode.BoxMembershipNotFound)
            return HttpErrors.Box.MemberNotFound(boxExternalId, user.ExternalId);

        throw new UnexpectedOperationResultException(
            operationName: nameof(LeaveBoxMembershipQuery),
            resultValueStr: result.ToString());
    }

    // Continue with all other endpoint handlers following the same pattern...
    // I'll show one more example and the rest would follow the same structure

    private static async Task<Results<Ok, NotFound<HttpError>>> AcceptBoxInvitation(
        [FromRoute] BoxExtId boxExternalId,
        HttpContext httpContext,
        BoxMembershipCache boxMembershipCache,
        AcceptBoxInvitationQuery acceptBoxInvitationQuery,
        CancellationToken cancellationToken)
    {
        var user = httpContext.GetUserContext();

        var boxMembership = await boxMembershipCache.TryGetBoxMembership(
            boxExternalId,
            user.Id,
            cancellationToken);

        if (boxMembership is null)
            return HttpErrors.Box.MemberNotFound(boxExternalId, user.ExternalId);

        var result = await acceptBoxInvitationQuery.Execute(
            boxMembership: boxMembership,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        if (result == AcceptBoxInvitationQuery.ResultCode.Ok)
        {
            await boxMembershipCache.InvalidateEntry(boxMembership, cancellationToken);
            return TypedResults.Ok();
        }

        if (result == AcceptBoxInvitationQuery.ResultCode.BoxInvitationNotFound)
            return HttpErrors.Box.InvitationNotFound(boxExternalId);

        throw new UnexpectedOperationResultException(
            operationName: nameof(AcceptBoxInvitationQuery),
            resultValueStr: result.ToString());
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> RejectBoxInvitation(
            [FromRoute] BoxExtId boxExternalId,
            HttpContext httpContext,
            BoxMembershipCache boxMembershipCache,
            RejectBoxInvitationQuery rejectBoxInvitationQuery,
            CancellationToken cancellationToken)
    {
        var user = httpContext.GetUserContext();

        var boxMembership = await boxMembershipCache.TryGetBoxMembership(
            boxExternalId,
            user.Id,
            cancellationToken);

        if (boxMembership is null)
            return HttpErrors.Box.MemberNotFound(boxExternalId, user.ExternalId);

        var result = await rejectBoxInvitationQuery.Execute(
            boxMembership: boxMembership,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        if (result == RejectBoxInvitationQuery.ResultCode.Ok)
        {
            await boxMembershipCache.InvalidateEntry(boxMembership, cancellationToken);
            return TypedResults.Ok();
        }

        if (result == RejectBoxInvitationQuery.ResultCode.BoxInvitationNotFound)
            return HttpErrors.Box.InvitationNotFound(boxExternalId);

        throw new UnexpectedOperationResultException(
            operationName: nameof(RejectBoxInvitationQuery),
            resultValueStr: result.ToString());
    }

    private static Results<Ok<GetBoxHtmlResponseDto>, NotFound<HttpError>> GetBoxHtml(
        HttpContext httpContext,
        BoxExternalAccessHandler boxExternalAccessHandler)
    {
        return boxExternalAccessHandler.GetBoxHtml(
            boxAccess: httpContext.GetBoxAccess());
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

    private static Results<Ok<GetFolderContentResponseDto>, NotFound<HttpError>> GetBoxContent(
        [FromRoute] FolderExtId? folderExternalId,
        HttpContext httpContext,
        BoxExternalAccessHandler boxExternalAccessHandler)
    {
        return boxExternalAccessHandler.GetBoxContent(
            httpContext: httpContext,
            boxAccess: httpContext.GetBoxAccess(),
            folderExternalId: folderExternalId);
    }

    private static Task<Results<Ok<GetBoxFileDownloadLinkResponseDto>, NotFound<HttpError>, BadRequest<HttpError>, StatusCodeHttpResult>> GetFileDownloadLink(
        [FromRoute] FileExtId fileExternalId,
        [FromQuery] string contentDisposition,
        HttpContext httpContext,
        BoxExternalAccessHandler boxExternalAccessHandler,
        CancellationToken cancellationToken)
    {
        return boxExternalAccessHandler.GetFileDownloadLink(
            fileExternalId: fileExternalId,
            contentDisposition: contentDisposition,
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
            boxAccess: httpContext.GetBoxAccess());
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

    private static Task<Results<Ok, StatusCodeHttpResult>> DeleteFile(
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

    private static Task<Results<Ok<BulkInitiateFileUploadResponseDto>, NotFound<HttpError>, StatusCodeHttpResult, BadRequest<HttpError>>> BulkInitiateFileUpload(
        HttpContext httpContext,
        BoxExternalAccessHandler boxExternalAccessHandler,
        CancellationToken cancellationToken)
    {
        var request = httpContext.GetProtobufRequest<BulkInitiateFileUploadRequestDto>();

        return boxExternalAccessHandler.BulkInitiateFileUpload(
            request: request,
            boxAccess: httpContext.GetBoxAccess(),
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

    private static Task<Results<Ok<InitiateBoxFilePartUploadResponseDto>, NotFound<HttpError>>> InitiateFilePartUpload(
        [FromRoute] FileUploadExtId fileUploadExternalId,
        [FromRoute] int partNumber,
        HttpContext httpContext,
        BoxExternalAccessHandler boxExternalAccessHandler,
        CancellationToken cancellationToken)
    {
        return boxExternalAccessHandler.InitiateFilePartUpload(
            fileUploadExternalId: fileUploadExternalId,
            partNumber: partNumber,
            boxAccess: httpContext.GetBoxAccess(),
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

    private static GetUploadsListResponseDto ListUploads(
        HttpContext httpContext,
        BoxExternalAccessHandler boxExternalAccessHandler)
    {
        return boxExternalAccessHandler.ListUploads(
            httpContext: httpContext,
            boxAccess: httpContext.GetBoxAccess());
    }
}