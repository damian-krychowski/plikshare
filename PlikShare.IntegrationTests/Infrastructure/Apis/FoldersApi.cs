using Flurl.Http;
using PlikShare.Folders.Create.Contracts;
using PlikShare.Folders.Id;
using PlikShare.Folders.List.Contracts;
using PlikShare.Folders.MoveToFolder.Contracts;
using PlikShare.Folders.Rename.Contracts;
using PlikShare.Workspaces.Id;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class FoldersApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<CreateFolderResponseDto> Create(
        CreateFolderRequestDto request,
        WorkspaceExtId workspaceExternalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery,
        Cookie? workspaceEncryptionSession = null)
    {
        return await flurlClient.ExecutePost<CreateFolderResponseDto, CreateFolderRequestDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/folders",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery,
            extraCookie: workspaceEncryptionSession);
    }

    public async Task<BulkCreateFolderResponseDto> BulkCreate(
        BulkCreateFolderRequestDto request,
        WorkspaceExtId workspaceExternalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery,
        Cookie? workspaceEncryptionSession = null)
    {
        return await flurlClient.ExecutePost<BulkCreateFolderResponseDto, BulkCreateFolderRequestDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/folders/bulk",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery,
            isRequestInProtobuf: true,
            isResponseInProtobuf: true,
            extraCookie: workspaceEncryptionSession);
    }

    public async Task<GetTopFolderContentResponseDto> GetTop(
        WorkspaceExtId workspaceExternalId,
        SessionAuthCookie? cookie,
        Cookie? workspaceEncryptionSession = null)
    {
        return await flurlClient.ExecuteGet<GetTopFolderContentResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/folders",
            cookie: cookie,
            isResponseInProtobuf: true,
            extraCookie: workspaceEncryptionSession);
    }

    public async Task<GetFolderContentResponseDto> Get(
        WorkspaceExtId workspaceExternalId,
        FolderExtId folderExternalId,
        SessionAuthCookie? cookie,
        Cookie? workspaceEncryptionSession = null)
    {
        return await flurlClient.ExecuteGet<GetFolderContentResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/folders/{folderExternalId}",
            cookie: cookie,
            isResponseInProtobuf: true,
            extraCookie: workspaceEncryptionSession);
    }

    public async Task UpdateName(
        WorkspaceExtId workspaceExternalId,
        FolderExtId folderExternalId,
        UpdateFolderNameRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery,
        Cookie? workspaceEncryptionSession = null)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/folders/{folderExternalId}/name",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery,
            extraCookie: workspaceEncryptionSession);
    }

    public async Task MoveItems(
        WorkspaceExtId workspaceExternalId,
        MoveItemsToFolderRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery,
        Cookie? workspaceEncryptionSession = null)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/folders/move-items",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery,
            extraCookie: workspaceEncryptionSession);
    }
}
