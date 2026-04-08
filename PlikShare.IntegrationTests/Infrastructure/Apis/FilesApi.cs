using Flurl.Http;
using PlikShare.Core.Utils;
using PlikShare.Files.Download.Contracts;
using PlikShare.Files.Id;
using PlikShare.Files.Preview.Comment.CreateComment.Contracts;
using PlikShare.Files.Preview.Comment.EditComment.Contracts;
using PlikShare.Files.Preview.GetDetails.Contracts;
using PlikShare.Files.Preview.GetZipContentDownloadLink.Contracts;
using PlikShare.Files.Preview.GetZipDetails.Contracts;
using PlikShare.Files.Preview.SaveNote.Contracts;
using PlikShare.Files.Rename.Contracts;
using PlikShare.Storages.Zip;
using PlikShare.Workspaces.Id;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class FilesApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task UpdateName(
        WorkspaceExtId workspaceExternalId,
        FileExtId fileExternalId,
        UpdateFileNameRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/files/{fileExternalId}/name",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task UpdateNote(
        WorkspaceExtId workspaceExternalId,
        FileExtId fileExternalId,
        SaveFileNoteRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/files/{fileExternalId}/note",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task CreateComment(
        WorkspaceExtId workspaceExternalId,
        FileExtId fileExternalId,
        CreateFileCommentRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePost(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/files/{fileExternalId}/comments",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task DeleteComment(
        WorkspaceExtId workspaceExternalId,
        FileExtId fileExternalId,
        FileArtifactExtId commentExternalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecuteDelete(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/files/{fileExternalId}/comments/{commentExternalId}",
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task EditComment(
        WorkspaceExtId workspaceExternalId,
        FileExtId fileExternalId,
        FileArtifactExtId commentExternalId,
        EditFileCommentRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/files/{fileExternalId}/comments/{commentExternalId}",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task UpdateContent(
        WorkspaceExtId workspaceExternalId,
        FileExtId fileExternalId,
        byte[] content,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        var response = await flurlClient
            .Request(appUrl, $"api/workspaces/{workspaceExternalId}/files/{fileExternalId}/content")
            .AllowAnyHttpStatus()
            .WithCookie(cookie)
            .WithAntiforgery(antiforgery)
            .PutAsync(new ByteArrayContent(content));

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            throw new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode);
        }
    }

    public async Task<GetFilePreviewDetailsResponseDto> GetPreviewDetails(
        WorkspaceExtId workspaceExternalId,
        FileExtId fileExternalId,
        string[] fields,
        SessionAuthCookie? cookie)
    {
        var fieldsQuery = string.Join("&", fields.Select(f => $"fields[]={f}"));

        return await flurlClient.ExecuteGet<GetFilePreviewDetailsResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/files/{fileExternalId}/preview/details?{fieldsQuery}",
            cookie: cookie);
    }

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
