using Flurl.Http;
using PlikShare.Agents.BoxAccess.Contracts;
using PlikShare.Agents.Create.Contracts;
using PlikShare.Agents.Get.Contracts;
using PlikShare.Agents.Id;
using PlikShare.Agents.List.Contracts;
using PlikShare.Agents.ListWorkspaceBoxes.Contracts;
using PlikShare.Agents.RotateToken.Contracts;
using PlikShare.Agents.UpdateSettings.Contracts;
using PlikShare.Boxes.Id;
using PlikShare.Workspaces.Id;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class AgentsApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<GetAgentsResponseDto> Get(
        SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetAgentsResponseDto>(
            appUrl: appUrl,
            apiPath: "api/agents",
            cookie: cookie);
    }

    public async Task<GetAgentDetails.ResponseDto> GetDetails(
        AgentExtId externalId,
        SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetAgentDetails.ResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/agents/{externalId}",
            cookie: cookie);
    }

    public async Task<CreateAgentResponseDto> Create(
        CreateAgentRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<CreateAgentResponseDto, CreateAgentRequestDto>(
            appUrl: appUrl,
            apiPath: "api/agents",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task Delete(
        AgentExtId externalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecuteDelete(
            appUrl: appUrl,
            apiPath: $"api/agents/{externalId}",
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task<RotateAgentTokenResponseDto> RotateToken(
        AgentExtId externalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<RotateAgentTokenResponseDto, object>(
            appUrl: appUrl,
            apiPath: $"api/agents/{externalId}/token/rotate",
            request: new object(),
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task UpdatePermissionsAndRoles(
        AgentExtId externalId,
        UpdateAgentPermissionsAndRolesRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/agents/{externalId}/permissions-and-roles",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task UpdateMaxWorkspaceNumber(
        AgentExtId externalId,
        UpdateAgentMaxWorkspaceNumberRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/agents/{externalId}/max-workspace-number",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task UpdateDefaultMaxWorkspaceSize(
        AgentExtId externalId,
        UpdateAgentDefaultMaxWorkspaceSizeRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/agents/{externalId}/default-max-workspace-size",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task UpdateDefaultMaxWorkspaceTeamMembers(
        AgentExtId externalId,
        UpdateAgentDefaultMaxWorkspaceTeamMembersRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/agents/{externalId}/default-max-workspace-team-members",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task UpdateStorageAccess(
        AgentExtId externalId,
        UpdateAgentStorageAccessRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/agents/{externalId}/storage-access",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task GrantWorkspaceAccess(
        AgentExtId externalId,
        WorkspaceExtId workspaceExternalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePut(
            appUrl: appUrl,
            apiPath: $"api/agents/{externalId}/workspaces/{workspaceExternalId}",
            request: new object(),
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task RevokeWorkspaceAccess(
        AgentExtId externalId,
        WorkspaceExtId workspaceExternalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecuteDelete(
            appUrl: appUrl,
            apiPath: $"api/agents/{externalId}/workspaces/{workspaceExternalId}",
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task GrantBoxAccess(
        AgentExtId externalId,
        BoxExtId boxExternalId,
        GrantAgentBoxAccessRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePut(
            appUrl: appUrl,
            apiPath: $"api/agents/{externalId}/boxes/{boxExternalId}",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task RevokeBoxAccess(
        AgentExtId externalId,
        BoxExtId boxExternalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecuteDelete(
            appUrl: appUrl,
            apiPath: $"api/agents/{externalId}/boxes/{boxExternalId}",
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task<ListWorkspaceBoxesResponseDto> ListWorkspaceBoxes(
        WorkspaceExtId workspaceExternalId,
        SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<ListWorkspaceBoxesResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/agents/workspaces/{workspaceExternalId}/boxes",
            cookie: cookie);
    }
}
