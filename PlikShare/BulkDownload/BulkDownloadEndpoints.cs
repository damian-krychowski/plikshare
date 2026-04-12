using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.AuditLog;
using PlikShare.AuditLog.Details;
using PlikShare.Core.Authorization;
using PlikShare.Core.Clock;
using PlikShare.Core.CORS;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Files.PreSignedLinks.Validation;
using PlikShare.Storages.Encryption.Authorization;
using PlikShare.Storages.Entities;
using PlikShare.Storages.HardDrive.BulkDownload;
using PlikShare.Storages.HardDrive.StorageClient;
using PlikShare.Storages.S3;
using PlikShare.Storages.S3.BulkDownload;
using PlikShare.Workspaces.Cache;
using Serilog;
using Serilog.Events;
using Audit = PlikShare.AuditLog.Details.Audit;

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
            .AddEndpointFilter<ValidateProtectedBulkDownloadPayloadFilter>()
            .WithName("BulkDownload");
    }

    private static async ValueTask<Results<EmptyHttpResult, BadRequest<HttpError>, NotFound<HttpError>, StatusCodeHttpResult>>
        BulkDownload(
            HttpContext httpContext,
            IClock clock,
            WorkspaceCache workspaceCache,
            BulkDownloadDetailsQuery bulkDownloadDetailsQuery,
            HardDriveBulkDownloadOperation hardDriveBulkDownloadOperation,
            S3BulkDownloadOperation s3BulkDownloadOperation,
            AuditLogService auditLogService,
            CancellationToken cancellationToken)
    {
        var payload = httpContext.GetProtectedBulkDownloadPayload();

        Log.Debug("Bulk download started: {@Payload}", payload);
        
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

        await auditLogService.Log(
            Audit.File.BulkDownloadedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                workspace: workspaceContext.ToAuditLogWorkspaceRef(),
                files: bulkDownloadDetails.Files.Select(f => new Audit.FileRef
                {
                    ExternalId = f.ExternalId,
                    Name = f.FullName,
                    SizeInBytes = f.SizeInBytes,
                    FolderPath = bulkDownloadDetails.FolderSubtree.GetFullPath(f.FolderId)
                }).ToList()),
            cancellationToken);

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
                        fullEncryptionSession: httpContext.TryGetFullEncryptionSession(),
                        s3StorageClient: s3StorageClient,
                        responsePipeWriter: httpContext.Response.BodyWriter,
                        cancellationToken: cancellationToken);
                    break;

                case HardDriveStorageClient hardDriveStorageClient:
                    await hardDriveBulkDownloadOperation.Execute(
                        bulkDownloadDetails: bulkDownloadDetails,
                        bucketName: workspaceContext.BucketName,
                        fullEncryptionSession: httpContext.TryGetFullEncryptionSession(),
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