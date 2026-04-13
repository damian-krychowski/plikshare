using Flurl.Http;
using PlikShare.Storages.FullEncryptionSessions;
using PlikShare.Storages.Id;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class FullEncryptionSessionsApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<GetFullEncryptionSessionsResponseDto> Get(
        SessionAuthCookie? cookie,
        params Cookie[] fullEncryptionSessions)
    {
        var request = flurlClient
            .Request(appUrl, "api/full-encryption-sessions/")
            .AllowAnyHttpStatus()
            .WithCookie(cookie);

        foreach (var session in fullEncryptionSessions)
        {
            request = request.WithCookie(session);
        }

        var response = await request.GetAsync();

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            throw new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode);
        }

        return (await response.GetJsonAsync<GetFullEncryptionSessionsResponseDto>())!;
    }

    public async Task Lock(
        StorageExtId storageExternalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery,
        params Cookie[] fullEncryptionSessions)
    {
        var request = flurlClient
            .Request(appUrl, $"api/full-encryption-sessions/{storageExternalId}")
            .AllowAnyHttpStatus()
            .WithCookie(cookie)
            .WithAntiforgery(antiforgery);

        foreach (var session in fullEncryptionSessions)
        {
            request = request.WithCookie(session);
        }

        var response = await request.DeleteAsync();

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            throw new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode);
        }
    }

    public async Task LockAll(
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery,
        params Cookie[] fullEncryptionSessions)
    {
        var request = flurlClient
            .Request(appUrl, "api/full-encryption-sessions/")
            .AllowAnyHttpStatus()
            .WithCookie(cookie)
            .WithAntiforgery(antiforgery);

        foreach (var session in fullEncryptionSessions)
        {
            request = request.WithCookie(session);
        }

        var response = await request.DeleteAsync();

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            throw new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode);
        }
    }
}
