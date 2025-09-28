using Flurl.Http;
using PlikShare.Folders.Create.Contracts;
using PlikShare.GeneralSettings;
using PlikShare.GeneralSettings.Contracts;

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
            apiPath: $"api/general-settings/application-sign-up",
            request: new SetSettingRequest
            {
                Value = value.Value
            },
            cookie: cookie,
            antiforgery: antiforgery);
    }
}