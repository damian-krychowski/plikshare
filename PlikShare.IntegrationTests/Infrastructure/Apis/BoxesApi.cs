using Flurl.Http;
using PlikShare.Boxes.Create.Contracts;
using PlikShare.Boxes.CreateLink.Contracts;
using PlikShare.Boxes.Get.Contracts;
using PlikShare.Boxes.Id;
using PlikShare.Boxes.List.Contracts;
using PlikShare.Boxes.UpdateFooter.Contracts;
using PlikShare.Boxes.UpdateFooterIsEnabled.Contracts;
using PlikShare.Boxes.UpdateHeader.Contracts;
using PlikShare.Boxes.UpdateHeaderIsEnabled.Contracts;
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