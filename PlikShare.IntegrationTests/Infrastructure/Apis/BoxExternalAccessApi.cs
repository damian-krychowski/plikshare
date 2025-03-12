using Flurl.Http;
using PlikShare.Boxes.Id;
using PlikShare.BoxExternalAccess.Contracts;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class BoxExternalAccessApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<GetBoxHtmlResponseDto> GetHtml(
        BoxExtId boxExternalId,
        SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetBoxHtmlResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/boxes/{boxExternalId}/html",
            cookie: cookie);
    }
}