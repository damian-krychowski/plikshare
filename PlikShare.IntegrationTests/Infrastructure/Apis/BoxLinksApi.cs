using Flurl.Http;
using PlikShare.BoxLinks.Id;
using PlikShare.BoxLinks.RegenerateAccessCode.Contracts;
using PlikShare.BoxLinks.UpdateIsEnabled.Contracts;
using PlikShare.BoxLinks.UpdateName.Contracts;
using PlikShare.BoxLinks.UpdatePermissions.Contracts;
using PlikShare.BoxLinks.UpdateWidgetOrigins.Contracts;
using PlikShare.Workspaces.Id;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class BoxLinksApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task UpdateName(
        WorkspaceExtId workspaceExternalId,
        BoxLinkExtId boxLinkExternalId,
        UpdateBoxLinkNameRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/box-links/{boxLinkExternalId}/name",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task UpdateWidgetOrigins(
        WorkspaceExtId workspaceExternalId,
        BoxLinkExtId boxLinkExternalId,
        UpdateBoxLinkWidgetOriginsRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/box-links/{boxLinkExternalId}/widget-origins",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task UpdateIsEnabled(
        WorkspaceExtId workspaceExternalId,
        BoxLinkExtId boxLinkExternalId,
        UpdateBoxLinkIsEnabledRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/box-links/{boxLinkExternalId}/is-enabled",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task UpdatePermissions(
        WorkspaceExtId workspaceExternalId,
        BoxLinkExtId boxLinkExternalId,
        UpdateBoxLinkPermissionsRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/box-links/{boxLinkExternalId}/permissions",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task<RegenerateBoxLinkAccessCodeResponseDto> RegenerateAccessCode(
        WorkspaceExtId workspaceExternalId,
        BoxLinkExtId boxLinkExternalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePatch<RegenerateBoxLinkAccessCodeResponseDto, object>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/box-links/{boxLinkExternalId}/regenerate-access-code",
            request: new { },
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task Delete(
        WorkspaceExtId workspaceExternalId,
        BoxLinkExtId boxLinkExternalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecuteDelete(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/box-links/{boxLinkExternalId}",
            cookie: cookie,
            antiforgery: antiforgery);
    }
}