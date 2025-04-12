using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.Core.Authorization;
using PlikShare.Core.Clock;
using PlikShare.Core.CORS;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Storages.Entities;
using PlikShare.Storages.HardDrive.BulkDownload;
using PlikShare.Storages.HardDrive.StorageClient;
using PlikShare.Storages.S3;
using PlikShare.Storages.S3.BulkDownload;
using PlikShare.Workspaces.Cache;
using Serilog;
using Serilog.Events;

// ReSharper disable PossibleMultipleEnumeration

namespace PlikShare.BulkDownload;

public static class BulkDownloadEndpoints
{
    public static void MapBulkDownloadEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/bulk-download")
            .WithTags("Bulk Download")
            .RequireAuthorization(policyNames: AuthPolicy.InternalOrBoxLink)
            .RequireCors(CorsPolicies.PreSignedLink);

        group.MapGet("/{protectedPayload}", BulkDownload)
            .WithName("BulkDownload");
    }

    private static async ValueTask<Results<EmptyHttpResult, BadRequest<HttpError>, NotFound<HttpError>, StatusCodeHttpResult>>
        BulkDownload(
            [FromRoute] string protectedPayload,
            HttpContext httpContext,
            IClock clock,
            WorkspaceCache workspaceCache,
            PreSignedUrlsService preSignedUrlsService,
            BulkDownloadDetailsQuery bulkDownloadDetailsQuery,
            HardDriveBulkDownloadOperation hardDriveBulkDownloadOperation,
            S3BulkDownloadOperation s3BulkDownloadOperation,
            CancellationToken cancellationToken)
    {
        var (extractionResult, payload) = preSignedUrlsService.TryExtractPreSignedBulkDownloadPayload(
            protectedPayload);

        if (extractionResult == PreSignedUrlsService.ExtractionResult.Invalid)
        {
            Log.Warning("An attempt to execute bulk download with invalid pre-signed url: {ProtectedPayload}",
                protectedPayload);

            return HttpErrors.BulkDownload.InvalidPayload();
        }

        if (extractionResult == PreSignedUrlsService.ExtractionResult.Expired)
        {
            Log.Warning("An attempt to execute bulk download with expired pre-signed url: {@Payload}",
                payload);

            return HttpErrors.BulkDownload.InvalidPayload();
        }

        var userIdentities = httpContext.User.GetUserIdentities();

        if (!userIdentities.ContainsIdentity(payload!.PreSignedBy))
        {
            Log.Warning(
                "An attempt to execute bulk download with pre-signed url by someone who is not the owner of the url. " +
                "Url Owner: {UrlOwner}, current user identities: {UserIdentities}", payload.PreSignedBy,
                userIdentities.ToList());

            return TypedResults.StatusCode(
                StatusCodes.Status403Forbidden);
        }

        if (extractionResult != PreSignedUrlsService.ExtractionResult.Ok)
            throw new InvalidOperationException($"Unrecognized ExtractionResul value: '{extractionResult}'");

        Log.Debug("Bulk download started: {@Payload}", payload);

        return await ExecuteBulkDownload(
            payload,
            httpContext,
            clock,
            workspaceCache,
            bulkDownloadDetailsQuery,
            hardDriveBulkDownloadOperation,
            s3BulkDownloadOperation,
            cancellationToken);
    }

    private static async Task<Results<EmptyHttpResult, BadRequest<HttpError>, NotFound<HttpError>, StatusCodeHttpResult>> ExecuteBulkDownload(
        PreSignedUrlsService.BulkDownloadPayload payload,
        HttpContext httpContext,
        IClock clock,
        WorkspaceCache workspaceCache,
        BulkDownloadDetailsQuery bulkDownloadDetailsQuery,
        HardDriveBulkDownloadOperation hardDriveBulkDownloadOperation,
        S3BulkDownloadOperation s3BulkDownloadOperation,
        CancellationToken cancellationToken)
    {
        var workspaceContext = await workspaceCache.TryGetWorkspace(
            payload.WorkspaceId,
            cancellationToken);

        if (workspaceContext is null)
        {
            Log.Warning("Could not execute bulk download with pre-signed url because Workspace#{WorkspaceId} was not found.",
                payload.WorkspaceId);

            return HttpErrors.BulkDownload.WorkspaceNotFound();
        }

        var bulkDownloadDetails = bulkDownloadDetailsQuery.GetDetailsFromDb(
            workspaceId: payload.WorkspaceId,
            selectedFileIds: payload.SelectedFileIds,
            excludedFileIds: payload.ExcludedFileIds,
            selectedFolderIds: payload.SelectedFolderIds,
            excludedFolderIds: payload.ExcludedFolderIds);

        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Bulk download will include following files: {Files}",
                bulkDownloadDetails.Files.Select(f => f.ExternalId));
        }

        httpContext.Response.Headers.ContentType = "application/zip";
        httpContext.Response.Headers.ContentDisposition = $"attachment; filename=bulk-download-{clock.UtcNow:yyyyMMddHHmmss}.zip";

        try
        {
            switch (workspaceContext.Storage)
            {
                case S3StorageClient s3StorageClient:
                    await s3BulkDownloadOperation.Execute(
                        bulkDownloadDetails: bulkDownloadDetails,
                        bucketName: workspaceContext.BucketName,
                        s3StorageClient: s3StorageClient,
                        responsePipeWriter: httpContext.Response.BodyWriter,
                        cancellationToken: cancellationToken);
                    break;

                case HardDriveStorageClient hardDriveStorageClient:
                    await hardDriveBulkDownloadOperation.Execute(
                        bulkDownloadDetails: bulkDownloadDetails,
                        bucketName: workspaceContext.BucketName,
                        hardDriveStorage: hardDriveStorageClient,
                        responsePipeWriter: httpContext.Response.BodyWriter,
                        cancellationToken: cancellationToken);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        paramName: typeof(StorageType).FullName,
                        actualValue: workspaceContext.Storage.GetType(),
                        message: $"Bulk delete cannot be performed due to unknown Storage#{workspaceContext.Storage.StorageId} type");
            }

            return TypedResults.Empty;
        }
        finally
        {
            await httpContext.Response.BodyWriter.CompleteAsync();
        }
    }
}