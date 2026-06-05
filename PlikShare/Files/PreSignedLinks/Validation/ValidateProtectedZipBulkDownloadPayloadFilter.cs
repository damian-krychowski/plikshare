using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Files.Records;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Files.PreSignedLinks.Validation;

public class ValidateProtectedZipBulkDownloadPayloadFilter : IEndpointFilter
{
    public const string ProtectedZipBulkDownloadPayloadContext = "protected-zip-bulk-download-payload-context";

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var protectedToken = context.HttpContext.Request.RouteValues["protectedPayload"]?.ToString();

        if (string.IsNullOrWhiteSpace(protectedToken))
            return HttpErrors.ProtectedPayload.Missing();

        var payload = context
            .HttpContext
            .RequestServices
            .GetRequiredService<PreSignedPayloadTokenStore>()
            .TryGet<PreSignedUrlsService.ZipBulkDownloadPayload>(protectedToken);

        if (payload is null)
        {
            Log.Warning("An attempt to execute zip bulk download with a forged, unknown or expired token.");

            return HttpErrors.ProtectedPayload.Invalid();
        }

        WorkspaceEncryptionSession? session = null;

        if (payload.WorkspaceDeks is { Length: > 0 })
        {
            var masterDataEncryption = context
                .HttpContext
                .RequestServices
                .GetRequiredService<IMasterDataEncryption>();

            session = payload.WorkspaceDeks.ToSession(
                masterDataEncryption);

            context.HttpContext.Items[WorkspaceEncryptionSession.HttpContextName] = session;
            context.HttpContext.Response.RegisterForDispose(session);
        }

        var getFileDetailsQuery = context
            .HttpContext
            .RequestServices
            .GetRequiredService<GetFilePreSignedDownloadLinkDetailsQuery>();

        var file = getFileDetailsQuery.Execute(
            fileExternalId: payload.FileExternalId,
            workspaceEncryptionSession: session);

        if (file.Code == GetFilePreSignedDownloadLinkDetailsQuery.ResultCode.NotFound)
        {
            Log.Warning("Could not execute zip bulk download with pre-signed url because File '{FileExternalId}' was not found.",
                payload.FileExternalId);

            return HttpErrors.File.NotFound(payload.FileExternalId);
        }

        if (session is not null && session.WorkspaceId != file.Details!.WorkspaceId)
        {
            Log.Warning("Could not execute zip bulk download with pre-signed url because File '{FileExternalId}' belongs to Workspace#{FileWorkspaceId} but pre-signed session is for Workspace#{SessionWorkspaceId}.",
                payload.FileExternalId, file.Details.WorkspaceId, session.WorkspaceId);

            return HttpErrors.File.NotFound(payload.FileExternalId);
        }

        var workspaceCache = context
            .HttpContext
            .RequestServices
            .GetRequiredService<WorkspaceCache>();

        var workspace = await workspaceCache.TryGetWorkspace(
            workspaceId: file.Details!.WorkspaceId,
            cancellationToken: context.HttpContext.RequestAborted);

        if (workspace is null)
        {
            Log.Warning("Could not execute zip bulk download with pre-signed url because Workspace#{WorkspaceId} was not found.",
                file.Details.WorkspaceId);

            return HttpErrors.Workspace.NotFound();
        }

        context.HttpContext.Items[ProtectedZipBulkDownloadPayloadContext] = new ProtectedZipBulkDownloadPayload(
            Payload: payload,
            File: file.Details,
            Workspace: workspace);

        return await next(context);
    }
}

public static class ProtectedZipBulkDownloadPayloadHttpContextExtensions
{
    public static ProtectedZipBulkDownloadPayload GetProtectedZipBulkDownloadPayload(this HttpContext httpContext)
    {
        var item = httpContext.Items[ValidateProtectedZipBulkDownloadPayloadFilter.ProtectedZipBulkDownloadPayloadContext];

        if (item is not ProtectedZipBulkDownloadPayload context)
        {
            throw new InvalidOperationException(
                "Cannot extract ProtectedZipBulkDownloadPayload from HttpContext.");
        }

        return context;
    }
}

public record ProtectedZipBulkDownloadPayload(
    PreSignedUrlsService.ZipBulkDownloadPayload Payload,
    FileRecord File,
    WorkspaceContext Workspace);
