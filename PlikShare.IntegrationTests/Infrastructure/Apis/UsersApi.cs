using Flurl.Http;
using PlikShare.Users.Invite.Contracts;
using PlikShare.Users.List.Contracts;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class UsersApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<GetUsersResponseDto> Get(SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetUsersResponseDto>(
            appUrl: appUrl,
            apiPath: "api/users",
            cookie: cookie);
    }

    public async Task<InviteUsersResponseDto> InviteUsers(
        InviteUsersRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<InviteUsersResponseDto, InviteUsersRequestDto>(
            appUrl: appUrl,
            apiPath: "api/users",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }
}