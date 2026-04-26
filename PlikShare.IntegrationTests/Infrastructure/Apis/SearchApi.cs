using Flurl.Http;
using PlikShare.Search.Get.Contracts;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class SearchApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<SearchResponseDto> Search(
        SearchRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery,
        Cookie? userEncryptionSession = null)
    {
        return await flurlClient.ExecutePost<SearchResponseDto, SearchRequestDto>(
            appUrl: appUrl,
            apiPath: "api/search",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery,
            isResponseInProtobuf: true,
            extraCookie: userEncryptionSession);
    }
}
