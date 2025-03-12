using Microsoft.AspNetCore.Http.HttpResults;
using PlikShare.Core.Utils;
using PlikShare.Core.Volumes;
using Serilog;

namespace PlikShare.GeneralSettings.LegalFiles;

public static class LegalFilesEndpoints
{
    public static void MapLegalFilesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/legal-files")
            .WithTags("Legal Files")
            .AllowAnonymous();

        group.MapGet("/terms-of-service", GetTermsOfServiceFile)
            .WithName("GetTermsOfServiceFile");

        group.MapGet("/privacy-policy", GetPrivacyPolicyFile)
            .WithName("GetPrivacyPolicyFile");
    }

    private static Results<FileStreamHttpResult, NotFound<HttpError>> GetTermsOfServiceFile(
        AppSettings appSettings,
        Volumes volumes)
    {
        if (appSettings.TermsOfService.FileName is null)
            return HttpErrors.LegalFiles.TermsOfServiceNotFound();

        return GetLegalFile(
            volumes,
            fileName: appSettings.TermsOfService.FileName,
            notFoundCode: "terms-of-service-not-found",
            notFoundMessage: "Terms of service file was not found");
    }

    private static Results<FileStreamHttpResult, NotFound<HttpError>> GetPrivacyPolicyFile(
        AppSettings appSettings,
        Volumes volumes)
    {
        if (appSettings.PrivacyPolicy.FileName is null)
            return HttpErrors.LegalFiles.PrivacyPolicyNotFound();

        return GetLegalFile(
            volumes,
            fileName: appSettings.PrivacyPolicy.FileName,
            notFoundCode: "privacy-policy-not-found",
            notFoundMessage: "Privacy policy file was not found");
    }

    private static Results<FileStreamHttpResult, NotFound<HttpError>> GetLegalFile(
        Volumes volumes,
        string fileName,
        string notFoundCode,
        string notFoundMessage)
    {
        var filePath = Path.Combine(
            volumes.Main.Legal.FullPath,
            fileName);

        if (!File.Exists(filePath))
        {
            Log.Warning("Legal file '{LegalFileName}' was not found even though it is configured and should be at '{LegalFileLocation}'.",
                fileName,
                filePath);

            return HttpErrors.LegalFiles.NotFound(notFoundCode, notFoundMessage);
        }

        return TypedResults.File(
            fileStream: new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                PlikShareStreams.DefaultBufferSize,
                true),
            contentType: "application/pdf",
            fileDownloadName: null,
            lastModified: null,
            entityTag: null,
            enableRangeProcessing: true);
    }
}