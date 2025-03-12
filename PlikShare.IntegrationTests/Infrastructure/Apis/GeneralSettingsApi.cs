using Flurl.Http;
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
}