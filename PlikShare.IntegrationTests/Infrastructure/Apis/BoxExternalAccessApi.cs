using Flurl.Http;
using PlikShare.Boxes.Id;
using PlikShare.BoxExternalAccess.Contracts;
using PlikShare.Files.Id;
using PlikShare.Workspaces.SearchFilesTree.Contracts;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class BoxExternalAccessApi(IFlurlClient flurlClient, string appUrl)
{
    /// <summary>
    /// Streams a Mini thumbnail bytes for a file the team-member can see in this box. Returns
    /// (statusCode, bytes). Body is empty on non-200 so negative-path tests can assert without
    /// throwing.
    /// </summary>
    public async Task<(int StatusCode, byte[] Body)> GetFileThumbnail(
        BoxExtId boxExternalId,
        FileExtId fileExternalId,
        SessionAuthCookie? cookie)
    {
        var response = await flurlClient
            .Request(appUrl, $"api/boxes/{boxExternalId}/files/{fileExternalId}/thumbnail")
            .AllowAnyHttpStatus()
            .WithCookies(cookie, null)
            .GetAsync();

        var body = response.ResponseMessage.IsSuccessStatusCode
            ? await response.GetBytesAsync()
            : [];

        return (response.StatusCode, body);
    }

    public async Task<GetBoxHtmlResponseDto> GetHtml(
        BoxExtId boxExternalId,
        SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetBoxHtmlResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/boxes/{boxExternalId}/html",
            cookie: cookie);
    }

    public async Task AcceptInvitation(
        BoxExtId boxExternalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePost(
            appUrl: appUrl,
            apiPath: $"api/boxes/{boxExternalId}/accept-invitation",
            request: new { },
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task RejectInvitation(
        BoxExtId boxExternalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePost(
            appUrl: appUrl,
            apiPath: $"api/boxes/{boxExternalId}/reject-invitation",
            request: new { },
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task LeaveBox(
        BoxExtId boxExternalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecuteDelete(
            appUrl: appUrl,
            apiPath: $"api/boxes/{boxExternalId}",
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task<SearchFilesTreeResponseDto> SearchFilesTree(
        BoxExtId boxExternalId,
        SearchFilesTreeRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<SearchFilesTreeResponseDto, SearchFilesTreeRequestDto>(
            appUrl: appUrl,
            apiPath: $"api/boxes/{boxExternalId}/search-files-tree",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery,
            isResponseInProtobuf: true);
    }
}