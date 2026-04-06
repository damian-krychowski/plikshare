using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.AuditLog;
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

    private static async Task<Results<Ok, BadRequest<HttpError>>> SetAlertOnNewUserRegistered(
        [FromBody] SetAlertSettingReuqest request,
        AppSettings appSettings,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        appSettings.SetAlertOnNewUserRegistered(
            isTurnedOn: request.IsTurnedOn);

        await auditLogService.Log(
            Audit.Settings.AlertOnNewUserChanged(
                actor: httpContext.GetAuditLogActorContext(),
                value: request.IsTurnedOn),
            cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, BadRequest<HttpError>>> SetNewUserDefaultPermissionsAndRoles(
        [FromBody] UserPermissionsAndRolesDto request,
        AppSettings appSettings,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var currentUser = httpContext.GetUserContext();

        var permissionsAndRoles = request.GetPermissionsAndRolesList();

        if (request.IsAdmin && !currentUser.Roles.IsAppOwner)
            return HttpErrors.User.OnlyAppOwnerCanAssignAdminRole();

        if (!request.IsAdmin && permissionsAndRoles.Any(Permissions.IsForAdminOnly))
            return HttpErrors.User.CannotAssignAdminPermissionToNonAdminUser();

        appSettings.SetNewUserPermissionsAndRoles(
            permissionsAndRoles);

        await auditLogService.Log(
            Audit.Settings.DefaultPermissionsChanged(
                actor: httpContext.GetAuditLogActorContext(),
                request: request),
            cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, BadRequest<HttpError>>> SetNewUserDefaultMaxWorkspaceTeamMembers(
        [FromBody] SetNewUserDefaultMaxWorkspaceTeamMembersRequestDto request,
        AppSettings appSettings,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        appSettings.SetNewUserDefaultMaxWorkspaceTeamMembers(request.Value);

        await auditLogService.Log(
            Audit.Settings.DefaultMaxWorkspaceTeamMembersChanged(
                actor: httpContext.GetAuditLogActorContext(),
                value: request.Value),
            cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, BadRequest<HttpError>>> SetNewUserDefaultMaxWorkspaceSizeInBytes(
        [FromBody] SetNewUserDefaultMaxWorkspaceSizeInBytesRequestDto request,
        AppSettings appSettings,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        appSettings.SetNewUserDefaultMaxWorkspaceSizeInBytes(request.Value);

        await auditLogService.Log(
            Audit.Settings.DefaultMaxWorkspaceSizeChanged(
                actor: httpContext.GetAuditLogActorContext(),
                value: request.Value),
            cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, BadRequest<HttpError>>> SetNewUserDefaultMaxWorkspaceNumber(
        [FromBody] SetNewUserDefaultMaxWorkspaceNumberRequestDto request,
        AppSettings appSettings,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        appSettings.SetNewUserDefaultMaxWorkspaceNumber(request.Value);

        await auditLogService.Log(
            Audit.Settings.DefaultMaxWorkspaceNumberChanged(
                actor: httpContext.GetAuditLogActorContext(),
                value: request.Value),
            cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task DeleteSignUpCheckbox(
        [FromRoute] int signUpCheckboxId,
        DeleteSignUpCheckboxQuery deleteSignUpCheckboxQuery,
        AppSettings appSettings,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
       await deleteSignUpCheckboxQuery.Execute(
            signUpCheckboxId,
            cancellationToken);

        appSettings.RefreshSingUpCheckboxes();

        await auditLogService.Log(
            Audit.Settings.SignUpCheckboxDeleted(
                actor: httpContext.GetAuditLogActorContext(),
                id: signUpCheckboxId),
            cancellationToken);
    }

    private static async Task<CreateOrUpdateSignUpCheckboxResponseDto> CreateOrUpdateSignUpCheckbox(
        [FromBody] CreateOrUpdateSignUpCheckboxRequestDto request,
        CreateOrUpdateSignUpCheckboxQuery createOrUpdateSignUpCheckboxQuery,
        AppSettings appSettings,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var response =  await createOrUpdateSignUpCheckboxQuery.Execute(
            request,
            cancellationToken);

        appSettings.RefreshSingUpCheckboxes();

        await auditLogService.Log(
            Audit.Settings.SignUpCheckboxCreatedOrUpdated(
                actor: httpContext.GetAuditLogActorContext(),
                id: request.Id,
                text: request.Text,
                isRequired: request.IsRequired),
            cancellationToken);

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
                CanManageUsers = appSettings.NewUserDefaultPermissionsAndRoles.CanManageUsers,
                CanManageAuth = appSettings.NewUserDefaultPermissionsAndRoles.CanManageAuth,
                CanManageIntegrations= appSettings.NewUserDefaultPermissionsAndRoles.CanManageIntegrations,
                CanManageAuditLog = appSettings.NewUserDefaultPermissionsAndRoles.CanManageAuditLog
            },
            AlertOnNewUserRegistered = appSettings.AlertOnNewUserRegistered.IsTurnedOn
        };
    }

    private static async Task<Results<Ok, BadRequest<HttpError>>> SetApplicationSignUpOption(
        [FromBody] SetSettingRequest request,
        AppSettings appSettings,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        if (AppSettings.SignUpSetting.TryParse(request.Value!, out var singUp))
        {
            appSettings.SetApplicationSignUp(singUp);

            await auditLogService.Log(
                Audit.Settings.SignUpOptionChanged(
                    actor: httpContext.GetAuditLogActorContext(),
                    value: request.Value!),
                cancellationToken);

            return TypedResults.Ok();
        }

        return HttpErrors.GeneralSettings.WrongApplicationSignUpValue(request.Value!);
    }

    private static async Task<Results<Ok, BadRequest<HttpError>>> SetApplicationName(
        [FromBody] SetSettingRequest request,
        AppSettings appSettings,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        appSettings.SetApplicationName(request.Value);

        await auditLogService.Log(
            Audit.Settings.AppNameChanged(
                actor: httpContext.GetAuditLogActorContext(),
                value: request.Value),
            cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, BadRequest<HttpError>>> UploadTermsOfService(
        IFormFile? file,
        AppSettings appSettings,
        UploadLegalFileOperation uploadLegalFileOperation,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
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

        await auditLogService.Log(
            Audit.Settings.TermsOfServiceUploaded(
                actor: httpContext.GetAuditLogActorContext()),
            cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task DeleteTermsOfService(
        DeleteLegalFileOperation deleteLegalFileOperation,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        deleteLegalFileOperation.ExecuteForTermsOfService();

        await auditLogService.Log(
            Audit.Settings.TermsOfServiceDeleted(
                actor: httpContext.GetAuditLogActorContext()),
            cancellationToken);
    }

    private static async Task<Results<Ok, BadRequest<HttpError>>> UploadPrivacyPolicy(
        IFormFile? file,
        AppSettings appSettings,
        UploadLegalFileOperation uploadLegalFileOperation,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
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

        await auditLogService.Log(
            Audit.Settings.PrivacyPolicyUploaded(
                actor: httpContext.GetAuditLogActorContext()),
            cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task DeletePrivacyPolicy(
        DeleteLegalFileOperation deleteLegalFileOperation,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        deleteLegalFileOperation.ExecuteForPrivacyPolicy();

        await auditLogService.Log(
            Audit.Settings.PrivacyPolicyDeleted(
                actor: httpContext.GetAuditLogActorContext()),
            cancellationToken);
    }
}