using Flurl.Http;
using PlikShare.Folders.Create.Contracts;
using PlikShare.Folders.Id;
using PlikShare.Folders.List.Contracts;
using PlikShare.Workspaces.Id;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class FoldersApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<CreateFolderResponseDto> Create(
        CreateFolderRequestDto request,
        WorkspaceExtId workspaceExternalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<CreateFolderResponseDto, CreateFolderRequestDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/folders",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task<BulkCreateFolderResponseDto> BulkCreate(
        BulkCreateFolderRequestDto request,
        WorkspaceExtId workspaceExternalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<BulkCreateFolderResponseDto, BulkCreateFolderRequestDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/folders/bulk",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery,
            isRequestInProtobuf: true,
            isResponseInProtobuf: true);
    }

    public async Task<GetTopFolderContentResponseDto> GetTop(
        WorkspaceExtId workspaceExternalId,
        SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetTopFolderContentResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/folders",
            cookie: cookie,
            isResponseInProtobuf: true);
    }
    
    public async Task<GetFolderContentResponseDto> Get(
        WorkspaceExtId workspaceExternalId,
        FolderExtId folderExternalId,
        SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetFolderContentResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/folders/{folderExternalId}",
            cookie: cookie,
            isResponseInProtobuf: true);
    }
}