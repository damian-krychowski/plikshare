using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.Core.Authorization;
using PlikShare.Core.Utils;
using PlikShare.GeneralSettings.Contracts;
using PlikShare.GeneralSettings.LegalFiles.DeleteLegalFile;
using PlikShare.GeneralSettings.LegalFiles.UploadLegalFile;
using PlikShare.GeneralSettings.SignUpCheckboxes.CreateOrUpdate;
using PlikShare.GeneralSettings.SignUpCheckboxes.CreateOrUpdate.Contracts;
using PlikShare.GeneralSettings.SignUpCheckboxes.Delete;
using PlikShare.Users.Middleware;
using PlikShare.Users.PermissionsAndRoles;

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

        group.MapPost("/sign-up-checkboxes", CreateOrUpdateSignUpCheckbox)
            .WithName("CreateOrUpdateSignUpCheckbox");

        group.MapDelete("/sign-up-checkboxes/{signUpCheckboxId}", DeleteSignUpCheckbox)
            .WithName("DeleteSignUpCheckbox");

        group.MapPatch("/new-user-default-max-workspace-number", SetNewUserDefaultMaxWorkspaceNumber)
            .WithName("SetNewUserDefaultMaxWorkspaceNumber");

        group.MapPatch("/new-user-default-max-workspace-size-in-bytes", SetNewUserDefaultMaxWorkspaceSizeInBytes)
            .WithName("SetNewUserDefaultMaxWorkspaceSizeInBytes");

        group.MapPatch("/new-user-default-max-workspace-team-members", SetNewUserDefaultMaxWorkspaceTeamMembers)
            .WithName("SetNewUserDefaultMaxWorkspaceTeamMembers");

        group.MapPatch("/new-user-default-permissions-and-roles", SetNewUserDefaultPermissionsAndRoles)
            .WithName("SetNewUserDefaultPermissionsAndRoles");

        group.MapPatch("/alert-on-new-user-registered", SetAlertOnNewUserRegistered)
            .WithName("SetAlertOnNewUserRegistered");
    }

    private static Results<Ok, BadRequest<HttpError>> SetAlertOnNewUserRegistered(
        [FromBody] SetAlertSettingReuqest request,
        AppSettings appSettings,
        HttpContext httpContext)
    {
        appSettings.SetAlertOnNewUserRegistered(
            isTurnedOn: request.IsTurnedOn);

        return TypedResults.Ok();
    }

    private static Results<Ok, BadRequest<HttpError>> SetNewUserDefaultPermissionsAndRoles(
        [FromBody] UserPermissionsAndRolesDto request,
        AppSettings appSettings,
        HttpContext httpContext)
    {
        var currentUser = httpContext.GetUserContext();

        var permissionsAndRoles = request.GetPermissionsAndRolesList();

        if (request.IsAdmin && !currentUser.Roles.IsAppOwner)
            return HttpErrors.User.OnlyAppOwnerCanAssignAdminRole();

        if (!request.IsAdmin && permissionsAndRoles.Any(Permissions.IsForAdminOnly))
            return HttpErrors.User.CannotAssignAdminPermissionToNonAdminUser();

        appSettings.SetNewUserPermissionsAndRoles(
            permissionsAndRoles);

        return TypedResults.Ok();
    }

    private static Results<Ok, BadRequest<HttpError>> SetNewUserDefaultMaxWorkspaceTeamMembers(
        [FromBody] SetNewUserDefaultMaxWorkspaceTeamMembersRequestDto request,
        AppSettings appSettings)
    {
        appSettings.SetNewUserDefaultMaxWorkspaceTeamMembers(request.Value);
        return TypedResults.Ok();
    }

    private static Results<Ok, BadRequest<HttpError>> SetNewUserDefaultMaxWorkspaceSizeInBytes(
        [FromBody] SetNewUserDefaultMaxWorkspaceSizeInBytesRequestDto request,
        AppSettings appSettings)
    {
        appSettings.SetNewUserDefaultMaxWorkspaceSizeInBytes(request.Value);
        return TypedResults.Ok();
    }

    private static Results<Ok, BadRequest<HttpError>> SetNewUserDefaultMaxWorkspaceNumber(
        [FromBody] SetNewUserDefaultMaxWorkspaceNumberRequestDto request,
        AppSettings appSettings)
    {
        appSettings.SetNewUserDefaultMaxWorkspaceNumber(request.Value);
        return TypedResults.Ok();
    }

    private static async Task DeleteSignUpCheckbox(
        [FromRoute] int signUpCheckboxId,
        DeleteSignUpCheckboxQuery deleteSignUpCheckboxQuery,
        AppSettings appSettings,
        CancellationToken cancellationToken)
    {
       await deleteSignUpCheckboxQuery.Execute(
            signUpCheckboxId,
            cancellationToken);

        appSettings.RefreshSingUpCheckboxes();
    }

    private static async Task<CreateOrUpdateSignUpCheckboxResponseDto> CreateOrUpdateSignUpCheckbox(
        [FromBody] CreateOrUpdateSignUpCheckboxRequestDto request,
        CreateOrUpdateSignUpCheckboxQuery createOrUpdateSignUpCheckboxQuery,
        AppSettings appSettings,
        CancellationToken cancellationToken)
    {
        var response =  await createOrUpdateSignUpCheckboxQuery.Execute(
            request,
            cancellationToken);

        appSettings.RefreshSingUpCheckboxes();

        return response;
    }

    private static GetApplicationSettingsResponse GetSettings(
        AppSettings appSettings)
    {
        return new GetApplicationSettingsResponse
        {
            ApplicationName = appSettings.ApplicationName.Name,
            ApplicationSignUp = appSettings.ApplicationSignUp.Value,
            PrivacyPolicy = appSettings.PrivacyPolicy.FileName,
            TermsOfService = appSettings.TermsOfService.FileName,
            SignUpCheckboxes = appSettings.SignUpCheckboxes.ToList(),
            NewUserDefaultMaxWorkspaceNumber = appSettings.NewUserDefaultMaxWorkspaceNumber.Value,
            NewUserDefaultMaxWorkspaceSizeInBytes = appSettings.NewUserDefaultMaxWorkspaceSizeInBytes.Value,
            NewUserDefaultMaxWorkspaceTeamMembers = appSettings.NewUserDefaultMaxWorkspaceTeamMembers.Value,
            NewUserDefaultPermissionsAndRoles = new UserPermissionsAndRolesDto
            {
                IsAdmin = appSettings.NewUserDefaultPermissionsAndRoles.IsAdmin,
                CanAddWorkspace = appSettings.NewUserDefaultPermissionsAndRoles.CanAddWorkspace,
                CanManageEmailProviders = appSettings.NewUserDefaultPermissionsAndRoles.CanManageEmailProviders,
                CanManageGeneralSettings = appSettings.NewUserDefaultPermissionsAndRoles.CanManageGeneralSettings,
                CanManageStorages = appSettings.NewUserDefaultPermissionsAndRoles.CanManageStorages,
                CanManageUsers = appSettings.NewUserDefaultPermissionsAndRoles.CanManageUsers
            },
            AlertOnNewUserRegistered = appSettings.AlertOnNewUserRegistered.IsTurnedOn
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