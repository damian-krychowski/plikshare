using Flurl.Http;
using PlikShare.Files.Id;
using PlikShare.QuickShareExternalAccess.Contracts;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class QuickShareExternalAccessApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<QuickShareInfoResult> GetInfo(
        string slug,
        string? token = null,
        Cookie? sessionCookie = null,
        Cookie? authCookie = null,
        bool allowErrors = false)
    {
        var apiPath = BuildPath($"api/quick-shares/{slug}/info", token);

        var response = await flurlClient
            .Request(appUrl, apiPath)
            .AllowAnyHttpStatus()
            .WithCookies(sessionCookie, authCookie)
            .GetAsync();

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            if (allowErrors)
            {
                return new QuickShareInfoResult(
                    Info: null,
                    SessionCookie: ExtractSessionCookie(response),
                    StatusCode: response.StatusCode,
                    ResponseBody: await response.GetStringAsync());
            }

            throw new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode,
                url: response.ResponseMessage.RequestMessage!.RequestUri!.AbsoluteUri);
        }

        var info = await response.GetJsonAsync<GetQuickShareInfoResponseDto>();

        return new QuickShareInfoResult(
            Info: info,
            SessionCookie: ExtractSessionCookie(response),
            StatusCode: response.StatusCode,
            ResponseBody: null);
    }

    public async Task<QuickShareUnlockResult> Unlock(
        string slug,
        UnlockQuickShareRequestDto request,
        AntiforgeryCookies antiforgery,
        string? token = null,
        Cookie? sessionCookie = null,
        bool allowErrors = false)
    {
        var apiPath = BuildPath($"api/quick-shares/{slug}/unlock", token);

        var response = await flurlClient
            .Request(appUrl, apiPath)
            .AllowAnyHttpStatus()
            .WithCookie(sessionCookie)
            .WithAntiforgery(antiforgery)
            .PostJsonAsync(request);

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            if (allowErrors)
            {
                return new QuickShareUnlockResult(
                    SessionCookie: ExtractSessionCookie(response),
                    StatusCode: response.StatusCode,
                    ResponseBody: await response.GetStringAsync());
            }

            throw new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode,
                url: response.ResponseMessage.RequestMessage!.RequestUri!.AbsoluteUri);
        }

        return new QuickShareUnlockResult(
            SessionCookie: ExtractSessionCookie(response),
            StatusCode: response.StatusCode,
            ResponseBody: null);
    }

    public async Task<GetQuickShareContentResponseDto> GetContent(
        string slug,
        string? token = null,
        Cookie? sessionCookie = null,
        Cookie? authCookie = null)
    {
        var apiPath = BuildPath($"api/quick-shares/{slug}/content", token);

        return await flurlClient.ExecuteGet<GetQuickShareContentResponseDto>(
            appUrl: appUrl,
            apiPath: apiPath,
            cookie: sessionCookie,
            extraCookie: authCookie);
    }

    public async Task<GetQuickShareBulkDownloadLinkResponseDto> GetBulkDownloadLink(
        string slug,
        AntiforgeryCookies antiforgery,
        GetQuickShareBulkDownloadLinkRequestDto? request = null,
        string? token = null,
        Cookie? sessionCookie = null,
        Cookie? authCookie = null)
    {
        var apiPath = BuildPath($"api/quick-shares/{slug}/bulk-download-link", token);

        return await flurlClient.ExecutePost<GetQuickShareBulkDownloadLinkResponseDto, object>(
            appUrl: appUrl,
            apiPath: apiPath,
            request: (object?)request ?? new { },
            cookie: sessionCookie,
            antiforgery: antiforgery,
            extraCookie: authCookie);
    }

    public async Task<GetQuickShareFileDownloadLinkResponseDto> GetFileDownloadLink(
        string slug,
        FileExtId fileExternalId,
        string contentDisposition,
        string? token = null,
        Cookie? sessionCookie = null,
        Cookie? authCookie = null)
    {
        var queryString = $"contentDisposition={contentDisposition}";
        if (!string.IsNullOrEmpty(token))
            queryString += $"&token={Uri.EscapeDataString(token)}";

        var apiPath = $"api/quick-shares/{slug}/files/{fileExternalId}/download-link?{queryString}";

        return await flurlClient.ExecuteGet<GetQuickShareFileDownloadLinkResponseDto>(
            appUrl: appUrl,
            apiPath: apiPath,
            cookie: sessionCookie,
            extraCookie: authCookie);
    }

    private static string BuildPath(string path, string? token)
    {
        return string.IsNullOrEmpty(token)
            ? path
            : $"{path}?token={Uri.EscapeDataString(token)}";
    }

    private static Cookie? ExtractSessionCookie(IFlurlResponse response)
    {
        // When the endpoint both creates a session and marks it unlocked it emits two
        // Set-Cookie headers with the same name; the last one wins (it carries the
        // unlocked timestamp). Use LastOrDefault to honor that ordering.
        var sessionCookie = response.Cookies
            .LastOrDefault(c => c.Name.StartsWith("qs_session_", StringComparison.Ordinal));

        return sessionCookie is null
            ? null
            : new GenericCookie(sessionCookie.Name, sessionCookie.Value);
    }

    public record QuickShareInfoResult(
        GetQuickShareInfoResponseDto? Info,
        Cookie? SessionCookie,
        int StatusCode,
        string? ResponseBody);

    public record QuickShareUnlockResult(
        Cookie? SessionCookie,
        int StatusCode,
        string? ResponseBody);
}
