using Flurl.Http;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Folders.Id;
using PlikShare.QuickShares;
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
    public async Task<CreatedQuickShare> Create(
        WorkspaceExtId workspaceExternalId,
        string name,
        List<FileExtId> selectedFiles,
        List<FolderExtId> selectedFolders,
        QuickShareMode mode,
        bool allowIndividualFileDownload,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery,
        string? customSlug = null,
        List<FileExtId>? excludedFiles = null,
        List<FolderExtId>? excludedFolders = null,
        DateTimeOffset? expiresAt = null,
        string? password = null,
        int? maxDownloads = null)
    {
        var response = await flurlClient.ExecutePost<CreateQuickShareResponseDto, CreateQuickShareRequestDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/quick-shares",
            request: new CreateQuickShareRequestDto
            {
                Name = name,
                CustomSlug = customSlug,
                SelectedFiles = selectedFiles.Select(x => x.Value).ToList(),
                SelectedFolders = selectedFolders.Select(x => x.Value).ToList(),
                ExcludedFiles = (excludedFiles ?? []).Select(x => x.Value).ToList(),
                ExcludedFolders = (excludedFolders ?? []).Select(x => x.Value).ToList(),
                Mode = mode.ToKebabCase(),
                AllowIndividualFileDownload = allowIndividualFileDownload,
                ExpiresAt = expiresAt?.ToString("O"),
                Password = password,
                MaxDownloads = maxDownloads
            },
            cookie: cookie,
            antiforgery: antiforgery,
            isRequestInProtobuf: true,
            isResponseInProtobuf: true);

        return new CreatedQuickShare(
            ExternalId: new QuickShareExtId(response.ExternalId),
            Slug: response.Slug,
            Url: response.Url);
    }

    public record CreatedQuickShare(
        QuickShareExtId ExternalId,
        string Slug,
        string Url);

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
