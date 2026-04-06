using Flurl.Http;
using PlikShare.Workspaces.ChangeOwner.Contracts;
using PlikShare.Workspaces.Id;
using PlikShare.Workspaces.UpdateMaxSize.Contracts;
using PlikShare.Workspaces.UpdateMaxTeamMembers.Contracts;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class WorkspacesAdminApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task UpdateOwner(
        WorkspaceExtId externalId,
        ChangeWorkspaceOwnerRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{externalId.Value}/owner",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
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
}
