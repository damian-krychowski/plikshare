using Flurl.Http;
using PlikShare.QuickShares.Create.Contracts;
using PlikShare.QuickShares.Get.Contracts;
using PlikShare.QuickShares.Id;
using PlikShare.QuickShares.List.Contracts;
using PlikShare.QuickShares.UpdateExpiration.Contracts;
using PlikShare.QuickShares.UpdateItems.Contracts;
using PlikShare.QuickShares.UpdateMaxDownloads.Contracts;
using PlikShare.QuickShares.UpdateMode.Contracts;
using PlikShare.QuickShares.UpdateName.Contracts;
using PlikShare.QuickShares.UpdatePassword.Contracts;
using PlikShare.QuickShares.UpdateSlug.Contracts;
using PlikShare.Workspaces.Id;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class QuickSharesApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<CreateQuickShareResponseDto> Create(
        WorkspaceExtId workspaceExternalId,
        CreateQuickShareRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<CreateQuickShareResponseDto, CreateQuickShareRequestDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/quick-shares",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task<GetQuickSharesResponseDto> GetList(
        WorkspaceExtId workspaceExternalId,
        SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetQuickSharesResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/quick-shares",
            cookie: cookie);
    }

    public async Task<GetQuickShareResponseDto> Get(
        WorkspaceExtId workspaceExternalId,
        QuickShareExtId quickShareExternalId,
        SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetQuickShareResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/quick-shares/{quickShareExternalId}",
            cookie: cookie);
    }

    public async Task Delete(
        WorkspaceExtId workspaceExternalId,
        QuickShareExtId quickShareExternalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecuteDelete(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/quick-shares/{quickShareExternalId}",
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task UpdateName(
        WorkspaceExtId workspaceExternalId,
        QuickShareExtId quickShareExternalId,
        UpdateQuickShareNameRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/quick-shares/{quickShareExternalId}/name",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task UpdateSlug(
        WorkspaceExtId workspaceExternalId,
        QuickShareExtId quickShareExternalId,
        UpdateQuickShareSlugRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/quick-shares/{quickShareExternalId}/slug",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task UpdateExpiration(
        WorkspaceExtId workspaceExternalId,
        QuickShareExtId quickShareExternalId,
        UpdateQuickShareExpirationRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/quick-shares/{quickShareExternalId}/expiration",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task UpdatePassword(
        WorkspaceExtId workspaceExternalId,
        QuickShareExtId quickShareExternalId,
        UpdateQuickSharePasswordRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/quick-shares/{quickShareExternalId}/password",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task UpdateMaxDownloads(
        WorkspaceExtId workspaceExternalId,
        QuickShareExtId quickShareExternalId,
        UpdateQuickShareMaxDownloadsRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/quick-shares/{quickShareExternalId}/max-downloads",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task UpdateMode(
        WorkspaceExtId workspaceExternalId,
        QuickShareExtId quickShareExternalId,
        UpdateQuickShareModeRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/quick-shares/{quickShareExternalId}/mode",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task UpdateItems(
        WorkspaceExtId workspaceExternalId,
        QuickShareExtId quickShareExternalId,
        UpdateQuickShareItemsRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/quick-shares/{quickShareExternalId}/items",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }
}
