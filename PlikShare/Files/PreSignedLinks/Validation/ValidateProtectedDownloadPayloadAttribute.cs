using PlikShare.Core.Authorization;
using PlikShare.Core.Encryption;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.Records;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Files.PreSignedLinks.Validation;

public class ValidateProtectedDownloadPayloadFilter : IEndpointFilter
{
    public const string ProtectedDownloadPayloadContext = "protected-download-payload-context";

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var protectedPayload = context.HttpContext.Request.RouteValues["protectedPayload"]?.ToString();

        if (string.IsNullOrWhiteSpace(protectedPayload))
        {
            return HttpErrors.ProtectedPayload.Missing();
        }

        var (extractionResult, payload) = context
            .HttpContext
            .RequestServices
            .GetRequiredService<PreSignedUrlsService>()
            .TryExtractPreSignedDownloadPayload(
                protectedDataUrlEncoded: protectedPayload);

        if (extractionResult == PreSignedUrlsService.ExtractionResult.Invalid)
        {
            Log.Warning("An attempt to execute file download with invalid pre-signed url: {ProtectedPayload}",
                protectedPayload);

            return HttpErrors.ProtectedPayload.Invalid();
        }

        if (extractionResult == PreSignedUrlsService.ExtractionResult.Expired)
        {
            Log.Warning("An attempt to execute file download with expired pre-signed url: {Payload}",
                payload);

            return HttpErrors.ProtectedPayload.Invalid();
        }

        var userIdentities = context
            .HttpContext
            .User
            .GetUserIdentities();

        if (!userIdentities.ContainsIdentity(payload!.PreSignedBy))
        {
            Log.Warning("An attempt to execute file download with pre-signed url by someone who is not the owner of the url. " +
                        "Url Owner: {UrlOwner}, current user identities: {UserIdentities}", payload.PreSignedBy, userIdentities.ToList());

            return TypedResults.StatusCode(
                StatusCodes.Status403Forbidden);
        }

        if (extractionResult != PreSignedUrlsService.ExtractionResult.Ok)
            throw new InvalidOperationException(
                $"Unrecognized ExtractionResul value: '{extractionResult}co'");

        WorkspaceEncryptionSession? session = null;

        if (payload.WorkspaceDeks is {Length: >0})
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
            Log.Warning("Could not execute file download with pre-signed url because File '{FileExternalId}' was not found.",
                payload.FileExternalId);

            return HttpErrors.File.NotFound(payload.FileExternalId);
        }

        if (session is not null && session.WorkspaceId != file.Details!.WorkspaceId)
        {
            Log.Warning("Could not execute file download with pre-signed url because File '{FileExternalId}' belongs to Workspace#{FileWorkspaceId} but pre-signed session is for Workspace#{SessionWorkspaceId}.",
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
            Log.Warning("Could not execute file download with pre-signed url because Workspace#{WorkspaceId} was not found.",
                file.Details.WorkspaceId);

            return HttpErrors.Workspace.NotFound();
        }

        context.HttpContext.Items[ProtectedDownloadPayloadContext] = new ProtectedDownloadPayload(
            Payload: payload,
            File: file.Details,
            Workspace: workspace);

        return await next(context);
    }
}

public static class ProtectedDownloadPayloadHttpContextExtensions
{
    public static ProtectedDownloadPayload GetProtectedDownloadPayload(this HttpContext httpContext)
    {
        var uploadPayload = httpContext.Items[ValidateProtectedDownloadPayloadFilter.ProtectedDownloadPayloadContext];

        if (uploadPayload is not ProtectedDownloadPayload context)
        {
            throw new InvalidOperationException(
                $"Cannot extract ProtectedDownloadPayloadContext from HttpContext.");
        }

        return context;
    }
}

public record ProtectedDownloadPayload(
    PreSignedUrlsService.DownloadPayload Payload,
    FileRecord File,
    WorkspaceContext Workspace);