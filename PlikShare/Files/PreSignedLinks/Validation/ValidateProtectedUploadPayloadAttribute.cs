using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Uploads.Algorithm;
using PlikShare.Uploads.Cache;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Files.PreSignedLinks.Validation;

public class ValidateProtectedUploadPayloadFilter : IEndpointFilter
{
    public const string ProtectedUploadPayloadContext = "protected-upload-payload-context";

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var protectedPayload = context.HttpContext.Request.RouteValues["protectedPayload"]?.ToString();

        if (string.IsNullOrWhiteSpace(protectedPayload))
            return HttpErrors.ProtectedPayload.Missing();

        if (context.HttpContext.Request.ContentLength == null)
        {
            Log.Warning("An attempt to execute file part upload with pre-signed without Content-Length request header, url: {ProtectedPayload}",
                protectedPayload);

            return HttpErrors.ProtectedPayload.MissingContentLengthHeader();
        }

        if (context.HttpContext.Request.ContentType == null)
        {
            Log.Warning("An attempt to execute file part upload with pre-signed without Content-Type request header, url: {ProtectedPayload}",
                protectedPayload);

            return HttpErrors.ProtectedPayload.MissingContentTypeHeader();
        }

        var (extractionResult, payload) = context
            .HttpContext
            .RequestServices
            .GetRequiredService<PreSignedUrlsService>()
            .TryExtractPreSignedUploadPayload(
                protectedPayload,
                context.HttpContext.Request.ContentType!);

        if (extractionResult == PreSignedUrlsService.ExtractionResult.Invalid)
        {
            Log.Warning("An attempt to execute file part upload with invalid pre-signed url: {ProtectedPayload}",
                protectedPayload);

            return HttpErrors.ProtectedPayload.Invalid();
        }

        if (extractionResult == PreSignedUrlsService.ExtractionResult.Expired)
        {
            Log.Warning("An attempt to execute file part upload with expired pre-signed url: {Payload}",
                payload);

            return HttpErrors.ProtectedPayload.Invalid();
        }

        if (extractionResult != PreSignedUrlsService.ExtractionResult.Ok)
            throw new InvalidOperationException(
                $"Unrecognized ExtractionResul value: '{extractionResult}'");
        
        var fileUploadCache = context
            .HttpContext
            .RequestServices
            .GetRequiredService<FileUploadCache>();

        var fileUpload = await fileUploadCache.GetFileUpload(
            uploadExternalId: payload!.FileUploadExternalId,
            cancellationToken: context.HttpContext.RequestAborted);

        if (fileUpload is null)
        {
            Log.Warning("Could not execute file part upload with pre-signed url because FileUpload '{FileUploadExternalId}' was not found.",
                payload.FileUploadExternalId);

            return HttpErrors.Upload.NotFound(payload.FileUploadExternalId);
        }

        if (fileUpload.UploadAlgorithm == UploadAlgorithm.DirectUpload &&
            fileUpload.FileToUpload.SizeInBytes != context.HttpContext.Request.ContentLength)
        {
            Log.Warning("Could not execute direct file upload with pre-signed url because content length {ContentLength} does not match expected file size {ExpectedSize}.",
                 context.HttpContext.Request.ContentLength, fileUpload.FileToUpload.SizeInBytes);

            return HttpErrors.Upload.InvalidContentLength(
                actual: context.HttpContext.Request.ContentLength,
                expected: fileUpload.FileToUpload.SizeInBytes);
        }

        var workspaceCache = context
            .HttpContext
            .RequestServices
            .GetRequiredService<WorkspaceCache>();

        var workspace = await workspaceCache.TryGetWorkspace(
            fileUpload.WorkspaceId,
            context.HttpContext.RequestAborted);

        if (workspace is null)
        {
            Log.Warning("Could not execute file part upload with pre-signed url because Workspace#{WorkspaceId} was not found.",
                fileUpload.WorkspaceId);

            return HttpErrors.Workspace.NotFound();
        }

        context.HttpContext.Items[ProtectedUploadPayloadContext] = new ProtectedUploadPayload(
            Payload: payload,
            FileUpload: fileUpload,
            Workspace: workspace);

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

public static class ProtectedUploadPayloadHttpContextExtensions
{
    public static ProtectedUploadPayload GetProtectedUploadPayload(this HttpContext httpContext)
    {
        var uploadPayload = httpContext.Items[ValidateProtectedUploadPayloadFilter.ProtectedUploadPayloadContext];

        if (uploadPayload is not ProtectedUploadPayload context)
        {
            throw new InvalidOperationException(
                $"Cannot extract ProtectedUploadPayloadContext from HttpContext.");
        }

        return context;
    }
}

public record ProtectedUploadPayload(
    PreSignedUrlsService.UploadPayload Payload,
    FileUploadContext FileUpload,
    WorkspaceContext Workspace);