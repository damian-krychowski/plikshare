using Flurl.Http;
using PlikShare.EntryPage.Contracts;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class EntryPageApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<GetEntryPageSettingsResponseDto> GetSettings(SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetEntryPageSettingsResponseDto>(
            appUrl: appUrl,
            apiPath: "api/entry-page",
            cookie: cookie);
    }
}