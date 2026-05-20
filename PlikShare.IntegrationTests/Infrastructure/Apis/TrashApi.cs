using Flurl.Http;
using PlikShare.Trash.DeleteForever.Contracts;
using PlikShare.Trash.List.Contracts;
using PlikShare.Trash.Restore.Contracts;
using PlikShare.Workspaces.Id;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class TrashApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<GetTrashItemsResponseDto> GetItems(
        WorkspaceExtId workspaceExternalId,
        SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetTrashItemsResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/trash",
            cookie: cookie);
    }

    public async Task<RestoreFromTrashResponseDto> Restore(
        WorkspaceExtId workspaceExternalId,
        RestoreFromTrashRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<RestoreFromTrashResponseDto, RestoreFromTrashRequestDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/trash/restore",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task<DeleteForeverResponseDto> DeleteForever(
        WorkspaceExtId workspaceExternalId,
        DeleteForeverRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<DeleteForeverResponseDto, DeleteForeverRequestDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/trash/items/delete-forever",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task<DeleteForeverResponseDto> EmptyTrash(
        WorkspaceExtId workspaceExternalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<DeleteForeverResponseDto, object>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/trash/empty",
            request: new object(),
            cookie: cookie,
            antiforgery: antiforgery);
    }
}
