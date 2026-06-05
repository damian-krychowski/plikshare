using PlikShare.Core.Encryption;
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
        var protectedToken = context.HttpContext.Request.RouteValues["protectedPayload"]?.ToString();

        if (string.IsNullOrWhiteSpace(protectedToken))
            return HttpErrors.ProtectedPayload.Missing();

        var payload = context
            .HttpContext
            .RequestServices
            .GetRequiredService<PreSignedPayloadTokenStore>()
            .TryGet<PreSignedUrlsService.BulkDownloadPayload>(protectedToken);

        if (payload is null)
        {
            Log.Warning("An attempt to execute bulk download with a forged, unknown or expired token.");

            return HttpErrors.BulkDownload.InvalidPayload();
        }

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
