using Flurl.Http;
using PlikShare.Locks.CheckFileLocks.Contracts;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class LockStatusApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<CheckFileLocksResponseDto> CheckFileLocks(
        CheckFileLocksRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<CheckFileLocksResponseDto, CheckFileLocksRequestDto>(
            appUrl: appUrl,
            apiPath: "api/lock-status/files",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }
}
