using System.Text.Json;
using Flurl.Http;
using PlikShare.Agents.Create.Contracts;
using PlikShare.Agents.Get.Contracts;
using PlikShare.Agents.Id;
using PlikShare.Agents.List.Contracts;
using PlikShare.Agents.ListWorkspaceBoxes.Contracts;
using PlikShare.Agents.Operations.List.Contracts;
using PlikShare.Agents.RotateToken.Contracts;
using PlikShare.Agents.Tools.Contracts;
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

    public async Task<GetAgentToolsResponseDto> GetTools(
        AgentExtId externalId,
        SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetAgentToolsResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/agents/{externalId}/tools",
            cookie: cookie);
    }

    public async Task UpdateToolConfig(
        AgentExtId externalId,
        string toolName,
        UpdateAgentToolConfigRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/agents/{externalId}/tools/{toolName}",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task ResetToolConfig(
        AgentExtId externalId,
        string toolName,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecuteDelete(
            appUrl: appUrl,
            apiPath: $"api/agents/{externalId}/tools/{toolName}",
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task<GetAgentWorkspaceToolsResponseDto> GetWorkspaceTools(
        AgentExtId externalId,
        WorkspaceExtId workspaceExternalId,
        SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetAgentWorkspaceToolsResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/agents/{externalId}/workspaces/{workspaceExternalId}/tools",
            cookie: cookie);
    }

    public async Task UpdateWorkspaceToolOverride(
        AgentExtId externalId,
        WorkspaceExtId workspaceExternalId,
        string toolName,
        UpdateAgentWorkspaceToolOverrideRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/agents/{externalId}/workspaces/{workspaceExternalId}/tools/{toolName}",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task ResetWorkspaceToolOverride(
        AgentExtId externalId,
        WorkspaceExtId workspaceExternalId,
        string toolName,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecuteDelete(
            appUrl: appUrl,
            apiPath: $"api/agents/{externalId}/workspaces/{workspaceExternalId}/tools/{toolName}",
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task<GetPendingAgentOperationsResponseDto> GetPendingOperations(
        SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetPendingAgentOperationsResponseDto>(
            appUrl: appUrl,
            apiPath: "api/agents/operations/pending",
            cookie: cookie);
    }

    public async Task<JsonElement> GetOperationDetails(
        string operationExternalId,
        SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<JsonElement>(
            appUrl: appUrl,
            apiPath: $"api/agents/operations/{operationExternalId}/details",
            cookie: cookie);
    }

    public async Task ApproveOperation(
        AgentExtId externalId,
        string operationExternalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePost(
            appUrl: appUrl,
            apiPath: $"api/agents/{externalId}/operations/{operationExternalId}/approve",
            request: new object(),
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task DenyOperation(
        AgentExtId externalId,
        string operationExternalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePost(
            appUrl: appUrl,
            apiPath: $"api/agents/{externalId}/operations/{operationExternalId}/deny",
            request: new object(),
            cookie: cookie,
            antiforgery: antiforgery);
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

    public async Task<ListWorkspaceBoxesResponseDto> ListWorkspaceBoxes(
        WorkspaceExtId workspaceExternalId,
        SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<ListWorkspaceBoxesResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/agents/workspaces/{workspaceExternalId}/boxes",
            cookie: cookie);
    }

    public async Task GrantBoxAccess(
        AgentExtId externalId,
        BoxExtId boxExternalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePut(
            appUrl: appUrl,
            apiPath: $"api/agents/{externalId}/boxes/{boxExternalId}",
            request: new object(),
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

    public async Task<GetAgentBoxToolsResponseDto> GetBoxTools(
        AgentExtId externalId,
        BoxExtId boxExternalId,
        SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetAgentBoxToolsResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/agents/{externalId}/boxes/{boxExternalId}/tools",
            cookie: cookie);
    }

    public async Task UpdateBoxToolOverride(
        AgentExtId externalId,
        BoxExtId boxExternalId,
        string toolName,
        UpdateAgentBoxToolOverrideRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/agents/{externalId}/boxes/{boxExternalId}/tools/{toolName}",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task ResetBoxToolOverride(
        AgentExtId externalId,
        BoxExtId boxExternalId,
        string toolName,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecuteDelete(
            appUrl: appUrl,
            apiPath: $"api/agents/{externalId}/boxes/{boxExternalId}/tools/{toolName}",
            cookie: cookie,
            antiforgery: antiforgery);
    }
}
