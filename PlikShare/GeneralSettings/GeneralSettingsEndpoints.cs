using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.Core.Authorization;
using PlikShare.Core.Utils;
using PlikShare.GeneralSettings.Contracts;
using PlikShare.GeneralSettings.LegalFiles.DeleteLegalFile;
using PlikShare.GeneralSettings.LegalFiles.UploadLegalFile;

namespace PlikShare.GeneralSettings;

public static class GeneralSettingsEndpoints
{
    public static void MapGeneralSettingsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/general-settings")
            .WithTags("General Settings")
            .RequireAuthorization(new AuthorizeAttribute
            {
                Policy = AuthPolicy.Internal,
                Roles = $"{Roles.Admin}"
            })
            .AddEndpointFilter(new RequireAdminPermissionEndpointFilter(Permissions.ManageGeneralSettings));

        group.MapGet("/", GetSettings)
            .WithName("GetSettings");

        group.MapPatch("/application-sign-up", SetApplicationSignUpOption)
            .WithName("SetApplicationSignUpOption");

        group.MapPatch("/application-name", SetApplicationName)
            .WithName("SetApplicationName");

        group.MapPost("/terms-of-service", UploadTermsOfService)
            .WithName("UploadTermsOfService");

        group.MapDelete("/terms-of-service", DeleteTermsOfService)
            .WithName("DeleteTermsOfService");

        group.MapPost("/privacy-policy", UploadPrivacyPolicy)
            .WithName("UploadPrivacyPolicy");

        group.MapDelete("/privacy-policy", DeletePrivacyPolicy)
            .WithName("DeletePrivacyPolicy");
    }

    private static GetApplicationSettingsResponse GetSettings(
        AppSettings appSettings)
    {
        return new GetApplicationSettingsResponse
        {
            ApplicationName = appSettings.ApplicationName.Name,
            ApplicationSignUp = appSettings.ApplicationSignUp.Value,
            PrivacyPolicy = appSettings.PrivacyPolicy.FileName,
            TermsOfService = appSettings.TermsOfService.FileName
        };
    }

    private static Results<Ok, BadRequest<HttpError>> SetApplicationSignUpOption(
        [FromBody] SetSettingRequest request,
        AppSettings appSettings)
    {
        if (AppSettings.SignUpSetting.TryParse(request.Value!, out var singUp))
        {
            appSettings.SetApplicationSignUp(singUp);
            return TypedResults.Ok();
        }

        return HttpErrors.GeneralSettings.WrongApplicationSignUpValue(request.Value!);
    }

    private static Results<Ok, BadRequest<HttpError>> SetApplicationName(
        [FromBody] SetSettingRequest request,
        AppSettings appSettings)
    {
        appSettings.SetApplicationName(request.Value);
        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, BadRequest<HttpError>>> UploadTermsOfService(
        IFormFile? file,
        AppSettings appSettings,
        UploadLegalFileOperation uploadLegalFileOperation)
    {
        if (file is null || file.Length == 0)
            return HttpErrors.GeneralSettings.FileIsNullOrEmpty();

        if (file.ContentType != "application/pdf")
            return HttpErrors.GeneralSettings.WrongFileType(
                "Terms of Service");

        if (file.FileName == appSettings.PrivacyPolicy.FileName)
            return HttpErrors.GeneralSettings.DuplicatedFileName(
                "Terms of Service", 
                "Privacy Policy");

        await uploadLegalFileOperation.ExecuteForTermsOfService(file: file);
        return TypedResults.Ok();
    }

    private static void DeleteTermsOfService(DeleteLegalFileOperation deleteLegalFileOperation)
    {
        deleteLegalFileOperation.ExecuteForTermsOfService();
    }

    private static async Task<Results<Ok, BadRequest<HttpError>>> UploadPrivacyPolicy(
        IFormFile? file,
        AppSettings appSettings,
        UploadLegalFileOperation uploadLegalFileOperation)
    {
        if (file is null || file.Length == 0)
            return HttpErrors.GeneralSettings.FileIsNullOrEmpty();

        if (file.ContentType != "application/pdf")
            return HttpErrors.GeneralSettings.WrongFileType(
                "Privacy Policy");

        if (file.FileName == appSettings.TermsOfService.FileName)
            return HttpErrors.GeneralSettings.DuplicatedFileName(
                "Privacy Policy", 
                "Terms of Service");

        await uploadLegalFileOperation.ExecuteForPrivacyPolicy(file: file);
        return TypedResults.Ok();
    }

    private static void DeletePrivacyPolicy(DeleteLegalFileOperation deleteLegalFileOperation)
    {
        deleteLegalFileOperation.ExecuteForPrivacyPolicy();
    }
}