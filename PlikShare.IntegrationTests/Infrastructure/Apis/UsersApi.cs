using Flurl.Http;
using PlikShare.Users.GetDetails.Contracts;
using PlikShare.Users.Id;
using PlikShare.Users.Invite.Contracts;
using PlikShare.Users.List.Contracts;
using PlikShare.Users.PermissionsAndRoles;

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

    public async Task<GetUserDetails.ResponseDto> GetDetails(
        UserExtId userExternalId,
        SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetUserDetails.ResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/users/{userExternalId}",
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

    public async Task UpdatePermissionsAndRoles(
        UserExtId userExternalId,
        UserPermissionsAndRolesDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/users/{userExternalId}/permissions-and-roles",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }
}