using Flurl.Http;
using PlikShare.Dashboard.Content.Contracts;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class DashboardApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<GetDashboardContentResponseDto> Get(SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetDashboardContentResponseDto>(
            appUrl: appUrl,
            apiPath: "api/dashboard",
            cookie: cookie,
            isResponseInProtobuf: true);
    }
}