using PlikShare.Core.Authorization;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Uploads.Algorithm;
using PlikShare.Uploads.Cache;
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

        var userIdentities = context
            .HttpContext
            .User
            .GetUserIdentities();

        if (!userIdentities.ContainsIdentity(payload!.PreSignedBy))
        {
            Log.Warning("An attempt to execute file part upload with pre-signed url by someone who is not the owner of the url. " +
                        "Url Owner: {UrlOwner}, current user identities: {UserIdentities}", payload.PreSignedBy, userIdentities.ToList());

            return TypedResults.StatusCode(
                StatusCodes.Status403Forbidden);
        }

        if (extractionResult != PreSignedUrlsService.ExtractionResult.Ok)
            throw new InvalidOperationException(
                $"Unrecognized ExtractionResul value: '{extractionResult}'");


        var fileUploadCache = context
            .HttpContext
            .RequestServices
            .GetRequiredService<FileUploadCache>();

        var fileUpload = await fileUploadCache.GetFileUpload(
            uploadExternalId: payload.FileUploadExternalId,
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

        context.HttpContext.Items[ProtectedUploadPayloadContext] = new ProtectedUploadPayload(
            Payload: payload,
            FileUpload: fileUpload);
        
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
    FileUploadContext FileUpload);