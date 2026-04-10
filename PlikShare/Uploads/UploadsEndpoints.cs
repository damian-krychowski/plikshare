using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.Core.Authorization;
using PlikShare.Core.CorrelationId;
using PlikShare.Core.Protobuf;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Uploads.Cache;
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
using PlikShare.AuditLog;
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
        AuditLogService auditLogService,
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

        switch (result.Code)
        {
            case BulkInitiateFileUploadOperation.ResultCode.Ok:
                await auditLogService.Log(
                    Audit.File.UploadInitiated(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: new AuditLogDetails.WorkspaceRef
                        {
                            ExternalId = workspaceMembership.Workspace.ExternalId,
                            Name = workspaceMembership.Workspace.Name
                        },
                        fileUploads: result.InitiatedFiles!.Select(f => new AuditLogDetails.FileUploadRef
                        {
                            ExternalId = f.FileUploadExternalId,
                            FileExternalId = f.FileExternalId,
                            Name = f.FileName,
                            SizeInBytes = f.SizeInBytes,
                            FolderPath = f.FolderPath
                        }).ToList()),
                    cancellationToken);

                return TypedResults.Ok(result.Response);

            case BulkInitiateFileUploadOperation.ResultCode.FoldersNotFound:
                return HttpErrors.Folder.NotFound(result.MissingFolders);

            case BulkInitiateFileUploadOperation.ResultCode.NotEnoughSpace:
                return HttpErrors.Workspace.NotEnoughSpace(workspaceMembership.Workspace.ExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(BulkInitiateFileUploadOperation),
                    resultValueStr: result.Code.ToString());
        }
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
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var result = await convertFileUploadToFileOperation.Execute(
            workspace: workspaceMembership.Workspace,
            fileUploadExternalId: fileUploadExternalId,
            userIdentity: new UserIdentity(workspaceMembership.User.ExternalId),
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case ConvertFileUploadToFileOperation.ResultCode.Ok:
                var fileUpload = result.FileUpload!;

                await auditLogService.Log(
                    Audit.File.UploadCompleted(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: new AuditLogDetails.WorkspaceRef
                        {
                            ExternalId = workspaceMembership.Workspace.ExternalId,
                            Name = workspaceMembership.Workspace.Name
                        },
                        fileUpload: new AuditLogDetails.FileUploadRef
                        {
                            ExternalId = fileUpload.ExternalId,
                            FileExternalId = fileUpload.FileToUpload.S3FileKey.FileExternalId,
                            Name = $"{fileUpload.FileName}{fileUpload.FileExtension}",
                            SizeInBytes = fileUpload.FileToUpload.SizeInBytes,
                            FolderPath = fileUpload.FolderAncestors.ToFolderPath()
                        }),
                    cancellationToken);

                return TypedResults.Ok(
                    new CompleteFileUploadResponseDto(
                        FileExternalId: fileUpload.FileToUpload.S3FileKey.FileExternalId));

            case ConvertFileUploadToFileOperation.ResultCode.FileUploadNotFound:
                return HttpErrors.Upload.NotFound(fileUploadExternalId);

            case ConvertFileUploadToFileOperation.ResultCode.FileUploadNotYetCompleted:
                return HttpErrors.Upload.NotCompleted(fileUploadExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(ConvertFileUploadToFileOperation),
                    resultValueStr: result.Code.ToString());
        }
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