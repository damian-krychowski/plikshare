using Flurl.Http;
using PlikShare.GeneralSettings;
using PlikShare.GeneralSettings.Contracts;
using PlikShare.GeneralSettings.SignUpCheckboxes.CreateOrUpdate.Contracts;
using PlikShare.Users.PermissionsAndRoles;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class GeneralSettingsApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<GetApplicationSettingsResponse> Get(SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetApplicationSettingsResponse>(
            appUrl: appUrl,
            apiPath: "api/general-settings",
            cookie: cookie);
    }

    public async Task SetApplicationSignUp(
        AppSettings.SignUpSetting value,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: "api/general-settings/application-sign-up",
            request: new SetSettingRequest
            {
                Value = value.Value
            },
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task SetApplicationName(
        string? name,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: "api/general-settings/application-name",
            request: new SetSettingRequest
            {
                Value = name
            },
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task SetNewUserDefaultMaxWorkspaceNumber(
        int? value,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: "api/general-settings/new-user-default-max-workspace-number",
            request: new SetNewUserDefaultMaxWorkspaceNumberRequestDto
            {
                Value = value
            },
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task SetNewUserDefaultMaxWorkspaceSizeInBytes(
        long? value,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: "api/general-settings/new-user-default-max-workspace-size-in-bytes",
            request: new SetNewUserDefaultMaxWorkspaceSizeInBytesRequestDto
            {
                Value = value
            },
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task SetNewUserDefaultMaxWorkspaceTeamMembers(
        int? value,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: "api/general-settings/new-user-default-max-workspace-team-members",
            request: new SetNewUserDefaultMaxWorkspaceTeamMembersRequestDto
            {
                Value = value
            },
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task SetNewUserDefaultPermissionsAndRoles(
        UserPermissionsAndRolesDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: "api/general-settings/new-user-default-permissions-and-roles",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task SetAlertOnNewUserRegistered(
        bool isTurnedOn,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: "api/general-settings/alert-on-new-user-registered",
            request: new SetAlertSettingReuqest
            {
                IsTurnedOn = isTurnedOn
            },
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task<CreateOrUpdateSignUpCheckboxResponseDto> CreateOrUpdateSignUpCheckbox(
        CreateOrUpdateSignUpCheckboxRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<CreateOrUpdateSignUpCheckboxResponseDto, CreateOrUpdateSignUpCheckboxRequestDto>(
            appUrl: appUrl,
            apiPath: "api/general-settings/sign-up-checkboxes",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task DeleteSignUpCheckbox(
        int signUpCheckboxId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecuteDelete(
            appUrl: appUrl,
            apiPath: $"api/general-settings/sign-up-checkboxes/{signUpCheckboxId}",
            cookie: cookie,
            antiforgery: antiforgery);
    }
}
