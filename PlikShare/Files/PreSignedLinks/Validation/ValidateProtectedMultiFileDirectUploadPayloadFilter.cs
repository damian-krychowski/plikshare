using PlikShare.Core.Authorization;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Files.PreSignedLinks.Validation;

public class ValidateProtectedMultiFileDirectUploadPayloadFilter : IEndpointFilter
{
    public const string ProtectedMultiFileDirectUploadPayloadContext = "protected-multi-file-direct-upload-payload-context";

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var protectedPayload = context.HttpContext.Request.RouteValues["protectedPayload"]?.ToString();

        if (string.IsNullOrWhiteSpace(protectedPayload))
        {
            return HttpErrors.ProtectedPayload.Missing();
        }

        if (context.HttpContext.Request.ContentLength == null)
        {
            Log.Warning("An attempt to execute multi-file-direct-upload with pre-signed without Content-Length request header, url: {ProtectedPayload}",
                protectedPayload);

            return HttpErrors.ProtectedPayload.MissingContentLengthHeader();
        }

        var (extractionResult, payload) = context
            .HttpContext
            .RequestServices
            .GetRequiredService<PreSignedUrlsService>()
            .TryExtractPreSignedMultiFileDirectUploadPayload(protectedPayload);

        if (extractionResult == PreSignedUrlsService.ExtractionResult.Invalid)
        {
            Log.Warning("An attempt to execute multi-file-direct-upload with invalid pre-signed url: {ProtectedPayload}",
                protectedPayload);

            return HttpErrors.ProtectedPayload.Invalid();
        }

        if (extractionResult == PreSignedUrlsService.ExtractionResult.Expired)
        {
            Log.Warning("An attempt to execute multi-file-direct-upload with expired pre-signed url: {Payload}",
                payload);

            return HttpErrors.ProtectedPayload.Invalid();
        }

        var userIdentities = context
            .HttpContext
            .User
            .GetUserIdentities();

        if (!userIdentities.ContainsIdentity(payload!.PreSignedBy))
        {
            Log.Warning("An attempt to execute multi-file-direct-upload with pre-signed url by someone who is not the owner of the url. " +
                        "Url Owner: {UrlOwner}, current user identities: {UserIdentities}", payload.PreSignedBy, userIdentities.ToList());

            return TypedResults.StatusCode(
                StatusCodes.Status403Forbidden);
        }

        if (extractionResult != PreSignedUrlsService.ExtractionResult.Ok)
            throw new InvalidOperationException(
                $"Unrecognized ExtractionResul value: '{extractionResult}'");

        var workspaceCache = context
            .HttpContext
            .RequestServices
            .GetRequiredService<WorkspaceCache>();

        var workspace = await workspaceCache.TryGetWorkspace(
            payload.WorkspaceId,
            context.HttpContext.RequestAborted);

        if (workspace is null)
        {
            Log.Warning("Could not execute multi-file-direct-upload with pre-signed url because Workspace#{WorkspaceId} was not found.",
                payload.WorkspaceId);

            return HttpErrors.Workspace.NotFound();
        }

        context.HttpContext.Items[ProtectedMultiFileDirectUploadPayloadContext] = new ProtectedMultiFileDirectUploadPayload(
            Payload: payload,
            Workspace: workspace);

        return await next(context);
    }
}

public static class ProtectedMultiFileDirectUploadPayloadHttpContextExtensions
{
    public static ProtectedMultiFileDirectUploadPayload GetProtectedMultiFileDirectUploadPayload(this HttpContext httpContext)
    {
        var uploadPayload = httpContext.Items[ValidateProtectedMultiFileDirectUploadPayloadFilter.ProtectedMultiFileDirectUploadPayloadContext];

        if (uploadPayload is not ProtectedMultiFileDirectUploadPayload context)
        {
            throw new InvalidOperationException(
                $"Cannot extract ProtectedMultiFileDirectUploadPayload from HttpContext.");
        }

        return context;
    }
}

public record ProtectedMultiFileDirectUploadPayload(
    PreSignedUrlsService.MultiFileDirectUploadPayload Payload,
    WorkspaceContext Workspace);