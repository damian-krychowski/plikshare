using Flurl.Http;
using PlikShare.BulkDelete.Contracts;
using PlikShare.Users.Id;
using PlikShare.Workspaces.Create.Contracts;
using PlikShare.Workspaces.Get.Contracts;
using PlikShare.Workspaces.Id;
using PlikShare.Workspaces.Members.AcceptInvitation.Contracts;
using PlikShare.Workspaces.Members.CreateInvitation.Contracts;
using PlikShare.Workspaces.Members.List.Contracts;
using PlikShare.Workspaces.Members.UpdatePermissions.Contracts;
using PlikShare.Workspaces.UpdateName.Contracts;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class WorkspacesApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<CreateWorkspaceResponseDto> Create(
        CreateWorkspaceRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<CreateWorkspaceResponseDto, CreateWorkspaceRequestDto>(
            appUrl: appUrl,
            apiPath: "api/workspaces",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task<GetWorkspaceDetailsResponseDto> GetDetails(
        WorkspaceExtId externalId,
        SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetWorkspaceDetailsResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{externalId.Value}",
            cookie: cookie);
    }

    public async Task UpdateName(
        WorkspaceExtId externalId,
        UpdateWorkspaceNameRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{externalId.Value}/name",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task Delete(
        WorkspaceExtId externalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecuteDelete(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{externalId.Value}",
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task<CreateWorkspaceMemberInvitationResponseDto> InviteMember(
        WorkspaceExtId externalId,
        CreateWorkspaceMemberInvitationRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<CreateWorkspaceMemberInvitationResponseDto, CreateWorkspaceMemberInvitationRequestDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{externalId.Value}/members",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task RevokeMember(
        WorkspaceExtId externalId,
        UserExtId memberExternalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecuteDelete(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{externalId.Value}/members/{memberExternalId.Value}",
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task UpdateMemberPermissions(
        WorkspaceExtId externalId,
        UserExtId memberExternalId,
        UpdateWorkspaceMemberPermissionsRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{externalId.Value}/members/{memberExternalId.Value}/permissions",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task<AcceptWorkspaceInvitationResponseDto> AcceptInvitation(
        WorkspaceExtId externalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<AcceptWorkspaceInvitationResponseDto, object>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{externalId.Value}/accept-invitation",
            request: new object(),
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task RejectInvitation(
        WorkspaceExtId externalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePost(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{externalId.Value}/reject-invitation",
            request: new object(),
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task<GetWorkspaceMembersListResponseDto> GetMembers(
        WorkspaceExtId externalId,
        SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetWorkspaceMembersListResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{externalId.Value}/members",
            cookie: cookie);
    }

    public async Task LeaveSharedWorkspace(
        WorkspaceExtId externalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePost(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{externalId.Value}/members/leave",
            request: new object(),
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task<BulkDeleteResponseDto> BulkDelete(
        WorkspaceExtId externalId,
        BulkDeleteRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<BulkDeleteResponseDto, BulkDeleteRequestDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{externalId.Value}/bulk-delete",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }
}