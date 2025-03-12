using PlikShare.Core.Authorization;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.Records;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Files.PreSignedLinks.Validation;

public class ValidateProtectedZipContentDownloadPayloadFilter : IEndpointFilter
{
    public const string ProtectedZipContentDownloadPayloadContext = "protected-zip-content-download-payload-context";

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var protectedPayload = context.HttpContext.Request.RouteValues["protectedPayload"]?.ToString();

        if (string.IsNullOrWhiteSpace(protectedPayload))
            return HttpErrors.ProtectedPayload.Missing();

        var (extractionResult, payload) = context
            .HttpContext
            .RequestServices
            .GetRequiredService<PreSignedUrlsService>()
            .TryExtractPreSignedZipContentDownloadPayload(protectedPayload);

        if (extractionResult == PreSignedUrlsService.ExtractionResult.Invalid)
        {
            Log.Warning("An attempt to execute zip content download with invalid pre-signed url: {ProtectedPayload}",
                protectedPayload);

            return HttpErrors.ProtectedPayload.Invalid();
        }

        if (extractionResult == PreSignedUrlsService.ExtractionResult.Expired)
        {
            Log.Warning("An attempt to execute zip content download with expired pre-signed url: {Payload}",
                payload);

            return HttpErrors.ProtectedPayload.Invalid();
        }

        var userIdentities = context
            .HttpContext
            .User
            .GetUserIdentities();

        if (!userIdentities.ContainsIdentity(payload!.PreSignedBy))
        {
            Log.Warning("An attempt to execute zip content download with pre-signed url by someone who is not the owner of the url. " +
                        "Url Owner: {UrlOwner}, current user identities: {UserIdentities}", payload.PreSignedBy, userIdentities.ToList());
            
            return TypedResults.StatusCode(
                StatusCodes.Status403Forbidden);
        }
        
        if (extractionResult != PreSignedUrlsService.ExtractionResult.Ok)
            throw new InvalidOperationException(
                $"Unrecognized ExtractionResul value: '{extractionResult}'");

        var getFileDetailsQuery = context
            .HttpContext
            .RequestServices
            .GetRequiredService<GetFilePreSignedDownloadLinkDetailsQuery>();

        var file = getFileDetailsQuery.Execute(
            fileExternalId: payload.FileExternalId);

        if (file.Code == GetFilePreSignedDownloadLinkDetailsQuery.ResultCode.NotFound)
        {
            Log.Warning("Could not execute zip content download with pre-signed url because File '{FileExternalId}' was not found.",
                payload.FileExternalId);

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
            Log.Warning("Could not execute file download with pre-signed url because Workspace#{WorkspaceId} was not found.",
                file.Details!.WorkspaceId);

            return HttpErrors.Workspace.NotFound();
        }

        context.HttpContext.Items[ProtectedZipContentDownloadPayloadContext] = new ProtectedZipContentDownloadPayload(
            Payload: payload,
            File : file.Details!,
            Workspace: workspace);

        return await next(context);
    }
}

public static class ProtectedZipContentDownloadPayloadHttpContextExtensions
{
    public static ProtectedZipContentDownloadPayload GetProtectedZipContentDownloadPayload(this HttpContext httpContext)
    {
        var uploadPayload = httpContext.Items[ValidateProtectedZipContentDownloadPayloadFilter.ProtectedZipContentDownloadPayloadContext];

        if (uploadPayload is not ProtectedZipContentDownloadPayload context)
        {
            throw new InvalidOperationException(
                $"Cannot extract ProtectedZipContentDownloadPayload from HttpContext.");
        }

        return context;
    }
}

public record ProtectedZipContentDownloadPayload(
    PreSignedUrlsService.ZipContentDownloadPayload Payload,
    FileRecord File,
    WorkspaceContext Workspace);