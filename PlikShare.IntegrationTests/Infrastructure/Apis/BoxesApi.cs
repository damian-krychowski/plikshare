using Flurl.Http;
using PlikShare.Boxes.Create.Contracts;
using PlikShare.Boxes.CreateLink.Contracts;
using PlikShare.Boxes.Get.Contracts;
using PlikShare.Boxes.Id;
using PlikShare.Boxes.List.Contracts;
using PlikShare.Boxes.Members.CreateInvitation.Contracts;
using PlikShare.Boxes.Members.UpdatePermissions.Contracts;
using PlikShare.Boxes.UpdateFolder.Contracts;
using PlikShare.Boxes.UpdateFooter.Contracts;
using PlikShare.Boxes.UpdateFooterIsEnabled.Contracts;
using PlikShare.Boxes.UpdateHeader.Contracts;
using PlikShare.Boxes.UpdateHeaderIsEnabled.Contracts;
using PlikShare.Boxes.UpdateIsEnabled.Contracts;
using PlikShare.Boxes.UpdateName.Contracts;
using PlikShare.Users.Id;
using PlikShare.Workspaces.Id;
using static PlikShare.Core.Utils.HttpErrors;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class BoxesApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<CreateBoxResponseDto> Create(
        WorkspaceExtId workspaceExternalId,
        CreateBoxRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<CreateBoxResponseDto, CreateBoxRequestDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/boxes",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }
    
    public async Task<GetBoxesResponseDto> GetList(
        WorkspaceExtId workspaceExternalId,
        SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetBoxesResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/boxes",
            cookie: cookie);
    }
    
    public async Task<GetBoxResponseDto> Get(
        WorkspaceExtId workspaceExternalId,
        BoxExtId boxExternalId,
        SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetBoxResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/boxes/{boxExternalId}",
            cookie: cookie);
    }
    
    public async Task UpdateHeaderIsEnabled(
        WorkspaceExtId workspaceExternalId,
        BoxExtId boxExternalId,
        UpdateBoxHeaderIsEnabledRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/boxes/{boxExternalId}/header/is-enabled",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }
    
    public async Task UpdateHeader(
        WorkspaceExtId workspaceExternalId,
        BoxExtId boxExternalId,
        UpdateBoxHeaderRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/boxes/{boxExternalId}/header",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }
    
    public async Task UpdateFooterIsEnabled(
        WorkspaceExtId workspaceExternalId,
        BoxExtId boxExternalId,
        UpdateBoxFooterIsEnabledRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/boxes/{boxExternalId}/footer/is-enabled",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }
    
    public async Task UpdateFooter(
        WorkspaceExtId workspaceExternalId,
        BoxExtId boxExternalId,
        UpdateBoxFooterRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/boxes/{boxExternalId}/footer",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task UpdateName(
        WorkspaceExtId workspaceExternalId,
        BoxExtId boxExternalId,
        UpdateBoxNameRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/boxes/{boxExternalId}/name",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task UpdateFolder(
        WorkspaceExtId workspaceExternalId,
        BoxExtId boxExternalId,
        UpdateBoxFolderRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/boxes/{boxExternalId}/folder",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task UpdateIsEnabled(
        WorkspaceExtId workspaceExternalId,
        BoxExtId boxExternalId,
        UpdateBoxIsEnabledRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/boxes/{boxExternalId}/is-enabled",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task Delete(
        WorkspaceExtId workspaceExternalId,
        BoxExtId boxExternalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecuteDelete(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/boxes/{boxExternalId}",
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task<CreateBoxInvitationResponseDto> InviteMember(
        WorkspaceExtId workspaceExternalId,
        BoxExtId boxExternalId,
        CreateBoxInvitationRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<CreateBoxInvitationResponseDto, CreateBoxInvitationRequestDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/boxes/{boxExternalId}/members",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task RevokeMember(
        WorkspaceExtId workspaceExternalId,
        BoxExtId boxExternalId,
        UserExtId memberExternalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecuteDelete(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/boxes/{boxExternalId}/members/{memberExternalId}",
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task UpdateMemberPermissions(
        WorkspaceExtId workspaceExternalId,
        BoxExtId boxExternalId,
        UserExtId memberExternalId,
        UpdateBoxMemberPermissionsRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/boxes/{boxExternalId}/members/{memberExternalId}/permissions",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task<CreateBoxLinkResponseDto> CreateBoxLink(
        WorkspaceExtId workspaceExternalId,
        BoxExtId boxExternalId,
        CreateBoxLinkRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<CreateBoxLinkResponseDto, CreateBoxLinkRequestDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/boxes/{boxExternalId}/box-links",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }
}