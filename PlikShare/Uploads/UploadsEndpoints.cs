using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.Core.Authorization;
using PlikShare.Core.CorrelationId;
using PlikShare.Core.Protobuf;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Uploads.CompleteFileUpload;
using PlikShare.Uploads.CompleteFileUpload.Contracts;
using PlikShare.Uploads.Count;
using PlikShare.Uploads.Count.Contracts;
using PlikShare.Uploads.FilePartUpload.Complete;
using PlikShare.Uploads.FilePartUpload.Complete.Contracts;
using PlikShare.Uploads.FilePartUpload.Initiate;
using PlikShare.Uploads.FilePartUpload.Initiate.Contracts;
using PlikShare.Uploads.GetDetails;
using PlikShare.Uploads.GetDetails.Contracts;
using PlikShare.Uploads.Id;
using PlikShare.Uploads.Initiate;
using PlikShare.Uploads.Initiate.Contracts;
using PlikShare.Uploads.List;
using PlikShare.Uploads.List.Contracts;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Validation;

namespace PlikShare.Uploads;

public static class UploadsEndpoints
{
    public static void MapUploadsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/workspaces/{workspaceExternalId}/uploads")
            .WithTags("Uploads")
            .RequireAuthorization(policyNames: AuthPolicy.Internal)
            .AddEndpointFilter<ValidateWorkspaceFilter>();

        // Base upload operations
        group.MapGet("/", ListUploads)
            .WithName("ListUploads");

        group.MapGet("/count", GetUploadsCount)
            .WithName("GetUploadsCount");

        // File upload initiation and details
        group.MapPost("/initiate/bulk", BulkInitiateFileUpload)
            .WithName("BulkInitiateFileUpload")
            .WithProtobufResponse();

        group.MapGet("/{fileUploadExternalId}", GetDetails)
            .WithName("GetFileUploadDetails");

        // File part operations
        group.MapPost("/{fileUploadExternalId}/parts/{partNumber:int}/initiate", InitiateFilePartUpload)
            .WithName("InitiateFilePartUpload");

        group.MapPost("/{fileUploadExternalId}/parts/{partNumber:int}/complete", CompleteFilePartUpload)
            .WithName("CompleteFilePartUpload");

        // Complete upload
        group.MapPost("/{fileUploadExternalId}/complete", CompleteUpload)
            .WithName("CompleteUpload");
    }

    private static async Task<Results<Ok<BulkInitiateFileUploadResponseDto>, NotFound<HttpError>, BadRequest<HttpError>>> BulkInitiateFileUpload(
        HttpContext httpContext,
        BulkInitiateFileUploadOperation bulkInitiateFileUploadOperation,
        WorkspaceCache workspaceCache,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        if (!workspaceMembership.Workspace.IsBucketCreated)
            return HttpErrors.Workspace.BucketNotReady(workspaceMembership.Workspace.ExternalId);

        var request = httpContext.GetProtobufRequest<BulkInitiateFileUploadRequestDto>();

        var result = await bulkInitiateFileUploadOperation.Execute(
            workspace: workspaceMembership.Workspace,
            fileDetailsList: request.Items,
            userIdentity: new UserIdentity(
                UserExternalId: workspaceMembership.User.ExternalId),
            boxFolderId: null,
            boxLinkId: null,
            cancellationToken: cancellationToken);

        await workspaceCache.InvalidateEntry(
            workspaceId: workspaceMembership.Workspace.Id,
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            BulkInitiateFileUploadOperation.ResultCode.Ok => TypedResults.Ok(
                result.Response),

            BulkInitiateFileUploadOperation.ResultCode.FoldersNotFound => HttpErrors.Folder.NotFound(
                result.MissingFolders),

            BulkInitiateFileUploadOperation.ResultCode.NotEnoughSpace => HttpErrors.Workspace.NotEnoughSpace(
                workspaceMembership.Workspace.ExternalId),
                
            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(BulkInitiateFileUploadOperation),
                resultValueStr: result.Code.ToString())
        };
    }

    private static Results<Ok<GetFileUploadDetailsResponseDto>, NotFound<HttpError>> GetDetails(
        [FromRoute] FileUploadExtId fileUploadExternalId,
        HttpContext httpContext,
        GetFileUploadDetailsQuery getFileUploadDetailsQuery,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var result = getFileUploadDetailsQuery.Execute(
            uploadExternalId: fileUploadExternalId,
            workspace: workspaceMembership.Workspace,
            userIdentity: new UserIdentity(
                workspaceMembership.User.ExternalId));

        return result.Code switch
        {
            GetFileUploadDetailsQuery.ResultCode.Ok =>
                TypedResults.Ok(new GetFileUploadDetailsResponseDto
                {
                    Algorithm = result.Details!.Algorithm,
                    ExpectedPartsCount = result.Details.ExpectedPartsCount,
                    AlreadyUploadedPartNumbers = result.Details.AlreadyUploadedPartNumbers
                }),

            GetFileUploadDetailsQuery.ResultCode.NotFound => 
                HttpErrors.Upload.NotFound(
                    fileUploadExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(GetFileUploadDetailsQuery),
                resultValueStr: result.Code.ToString())
        };
    }

    private static async Task<Results<Ok<InitiateFilePartUploadResponseDto>, NotFound<HttpError>>> InitiateFilePartUpload(
        [FromRoute] FileUploadExtId fileUploadExternalId,
        [FromRoute] int partNumber,
        HttpContext httpContext,
        InitiateFilePartUploadOperation initiateFilePartUploadOperation,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var result = await initiateFilePartUploadOperation.Execute(
            workspace: workspaceMembership.Workspace,
            fileUploadExternalId: fileUploadExternalId,
            partNumber: partNumber,
            boxLinkId: null,
            userIdentity: new UserIdentity(workspaceMembership.User.ExternalId),
            enforceInternalPassThrough: false,
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            InitiateFilePartUploadOperation.ResultCode.FilePartUploadInitiated =>
                TypedResults.Ok(new InitiateFilePartUploadResponseDto(
                    UploadPreSignedUrl: result.Details!.UploadPreSignedUrl,
                    StartsAtByte: result.Details.StartsAtByte,
                    EndsAtByte: result.Details.EndsAtByte,
                    IsCompleteFilePartUploadCallbackRequired: result.Details.IsCompleteFilePartUploadCallbackRequired)),

            InitiateFilePartUploadOperation.ResultCode.FileUploadNotFound => 
                HttpErrors.Upload.NotFound(
                    fileUploadExternalId),

            InitiateFilePartUploadOperation.ResultCode.FileUploadPartNumberNotAllowed => 
                HttpErrors.Upload.PartNotAllowed(
                    externalId: fileUploadExternalId,
                    partNumber: partNumber),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(InitiateFilePartUploadOperation),
                resultValueStr: result.Code.ToString())
        };
    }

    private static async ValueTask<Results<Ok, NotFound<HttpError>>> CompleteFilePartUpload(
        [FromRoute] FileUploadExtId fileUploadExternalId,
        [FromRoute] int partNumber,
        [FromBody] CompleteFilePartUploadRequestDto request,
        HttpContext httpContext,
        CompleteFilePartUploadQuery completeFilePartUploadQuery,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var result = await completeFilePartUploadQuery.Execute(
            workspace: workspaceMembership.Workspace,
            fileUploadExternalId: fileUploadExternalId,
            partNumber: partNumber,
            eTag: request.ETag,
            userIdentity: new UserIdentity(workspaceMembership.User.ExternalId),
            cancellationToken: cancellationToken);

        return result switch
        {
            CompleteFilePartUploadQuery.ResultCode.Ok => 
                TypedResults.Ok(),

            CompleteFilePartUploadQuery.ResultCode.FileUploadNotFound => 
                HttpErrors.Upload.NotFound(
                    fileUploadExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(CompleteFilePartUploadQuery),
                resultValueStr: result.ToString())
        };
    }

    private static async ValueTask<Results<Ok<CompleteFileUploadResponseDto>, BadRequest<HttpError>, NotFound<HttpError>>> CompleteUpload(
        [FromRoute] FileUploadExtId fileUploadExternalId,
        HttpContext httpContext,
        ConvertFileUploadToFileOperation convertFileUploadToFileOperation,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var result = await convertFileUploadToFileOperation.Execute(
            workspace: workspaceMembership.Workspace,
            fileUploadExternalId: fileUploadExternalId,
            userIdentity: new UserIdentity(workspaceMembership.User.ExternalId),
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            ConvertFileUploadToFileOperation.ResultCode.Ok => 
                TypedResults.Ok(
                    new CompleteFileUploadResponseDto(
                        FileExternalId: result.FileExternalId)),

            ConvertFileUploadToFileOperation.ResultCode.FileUploadNotFound => 
                HttpErrors.Upload.NotFound(
                    fileUploadExternalId),  

            ConvertFileUploadToFileOperation.ResultCode.FileUploadNotYetCompleted => 
                HttpErrors.Upload.NotCompleted(
                    fileUploadExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(ConvertFileUploadToFileOperation),
                resultValueStr: result.Code.ToString())
        };
    }

    private static GetUploadsListResponseDto ListUploads(
        HttpContext httpContext,
        GetUploadsListQuery getUploadsListQuery,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var response = getUploadsListQuery.Execute(
            workspace: workspaceMembership.Workspace,
            userIdentity: new UserIdentity(workspaceMembership.User.ExternalId),
            boxFolderId: null);

        return response;
    }

    private static GetUploadsCountResponse GetUploadsCount(
        HttpContext httpContext,
        GetUploadsCountQuery getUploadsCountQuery)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var result = getUploadsCountQuery.Execute(
            workspace: workspaceMembership.Workspace,
            user: workspaceMembership.User);

        return new GetUploadsCountResponse(
            Count: result.Value);
    }
}