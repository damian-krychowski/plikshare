using Flurl.Http;
using PlikShare.Core.Utils;
using PlikShare.Files.Download.Contracts;
using PlikShare.Files.Id;
using PlikShare.Files.Preview.GetZipContentDownloadLink.Contracts;
using PlikShare.Files.Preview.GetZipDetails.Contracts;
using PlikShare.Storages.Zip;
using PlikShare.Workspaces.Id;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class FilesApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<GetFileDownloadLinkResponseDto> GetDownloadLink(
        WorkspaceExtId workspaceExternalId,
        FileExtId fileExternalId,
        string contentDisposition,
        SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetFileDownloadLinkResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/files/{fileExternalId}/download-link?contentDisposition={contentDisposition}",
            cookie: cookie);
    }

    public async Task<GetZipFileDetailsResponseDto> GetZipDetails(
        WorkspaceExtId workspaceExternalId,
        FileExtId fileExternalId,
        SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetZipFileDetailsResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/files/{fileExternalId}/preview/zip",
            cookie: cookie,
            isResponseInProtobuf: true);
    }

    public async Task<GetZipContentDownloadLinkResponseDto> GetZipContentDownloadLink(
        WorkspaceExtId workspaceExternalId,
        FileExtId fileExternalId,
        ZipFileDto item,
        ContentDispositionType contentDisposition,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<GetZipContentDownloadLinkResponseDto, GetZipContentDownloadLinkRequestDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/files/{fileExternalId}/preview/zip/download-link",
            request: new GetZipContentDownloadLinkRequestDto(
                Item: item,
                ContentDisposition: contentDisposition),
            cookie: cookie,
            antiforgery: antiforgery);
    }
}
