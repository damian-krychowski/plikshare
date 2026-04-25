using PlikShare.Core.Authorization;
using PlikShare.Core.Encryption;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using Serilog;

namespace PlikShare.Files.PreSignedLinks.Validation;

public class ValidateProtectedBulkDownloadPayloadFilter : IEndpointFilter
{
    public const string ProtectedBulkDownloadPayloadContext = "protected-bulk-download-payload-context";

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
            .TryExtractPreSignedBulkDownloadPayload(protectedPayload);

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

        var userIdentities = context.HttpContext.User.GetUserIdentities();

        if (!userIdentities.ContainsIdentity(payload!.PreSignedBy))
        {
            Log.Warning(
                "An attempt to execute bulk download with pre-signed url by someone who is not the owner of the url. " +
                "Url Owner: {UrlOwner}, current user identities: {UserIdentities}",
                payload.PreSignedBy,
                userIdentities.ToList());

            return TypedResults.StatusCode(StatusCodes.Status403Forbidden);
        }

        if (extractionResult != PreSignedUrlsService.ExtractionResult.Ok)
            throw new InvalidOperationException(
                $"Unrecognized ExtractionResul value: '{extractionResult}'");

        context.HttpContext.Items[ProtectedBulkDownloadPayloadContext] = payload;

        if (payload.WorkspaceDeks is {Length: >0})
        {
            var masterDataEncryption = context
                .HttpContext
                .RequestServices
                .GetRequiredService<IMasterDataEncryption>();

            var session = payload.WorkspaceDeks.ToSession(
                masterDataEncryption);

            context.HttpContext.Items[WorkspaceEncryptionSession.HttpContextName] = session;
            context.HttpContext.Response.RegisterForDispose(session);
        }

        return await next(context);
    }
}

public static class ProtectedBulkDownloadPayloadHttpContextExtensions
{
    public static PreSignedUrlsService.BulkDownloadPayload GetProtectedBulkDownloadPayload(this HttpContext httpContext)
    {
        var payload = httpContext.Items[ValidateProtectedBulkDownloadPayloadFilter.ProtectedBulkDownloadPayloadContext];

        if (payload is not PreSignedUrlsService.BulkDownloadPayload context)
        {
            throw new InvalidOperationException(
                "Cannot extract BulkDownloadPayload from HttpContext.");
        }

        return context;
    }
}
