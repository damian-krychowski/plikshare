using Flurl.Http;
using PlikShare.Users.Id;
using PlikShare.Workspaces.ChangeOwner.Contracts;
using PlikShare.Workspaces.Id;
using PlikShare.Workspaces.ListAll.Contracts;
using PlikShare.Workspaces.Members.AdminAdd.Contracts;
using PlikShare.Workspaces.UpdateMaxSize.Contracts;
using PlikShare.Workspaces.UpdateMaxTeamMembers.Contracts;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class WorkspacesAdminApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task UpdateOwner(
        WorkspaceExtId externalId,
        ChangeWorkspaceOwnerRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery,
        Cookie? userEncryptionSession = null)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{externalId.Value}/owner",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery,
            extraCookie: userEncryptionSession);
    }

    public async Task UpdateMaxSize(
        WorkspaceExtId externalId,
        UpdateWorkspaceMaxSizeDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{externalId.Value}/max-size",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task UpdateMaxTeamMembers(
        WorkspaceExtId externalId,
        UpdateWorkspaceMaxTeamMembersRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{externalId.Value}/max-team-members",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task<GetAllWorkspacesResponseDto> ListAll(
        SessionAuthCookie? cookie,
        UserExtId? excludeMemberOrOwner = null)
    {
        var apiPath = "api/workspaces/admin-list-all";

        if (excludeMemberOrOwner is not null)
            apiPath += $"?excludeMemberOrOwnerExternalId={excludeMemberOrOwner.Value.Value}";

        return await flurlClient.ExecuteGet<GetAllWorkspacesResponseDto>(
            appUrl: appUrl,
            apiPath: apiPath,
            cookie: cookie);
    }

    public async Task<AdminAddWorkspaceMemberResponseDto> AssignMember(
        WorkspaceExtId externalId,
        AdminAddWorkspaceMemberRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery,
        Cookie? userEncryptionSession = null)
    {
        return await flurlClient.ExecutePost<AdminAddWorkspaceMemberResponseDto, AdminAddWorkspaceMemberRequestDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{externalId.Value}/members/assign",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery,
            extraCookie: userEncryptionSession);
    }
}
