using Flurl.Http;
using PlikShare.AuthProviders.Id;
using PlikShare.Core.Authorization;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class SsoApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<SsoInitiateResult> Initiate(
        AuthProviderExtId authProviderExternalId)
    {
        var response = await flurlClient
            .Request(appUrl, $"api/auth/sso/{authProviderExternalId}")
            .AllowAnyHttpStatus()
            .WithAutoRedirect(false)
            .GetAsync();

        var locationHeader = response.Headers
            .FirstOrDefault(h => h.Name.Equals("Location", StringComparison.OrdinalIgnoreCase))
            .Value;

        return new SsoInitiateResult(
            StatusCode: response.StatusCode,
            LocationHeader: locationHeader);
    }

    public async Task<SsoCallbackResult> Callback(string code, string state)
    {
        var response = await flurlClient
            .Request(appUrl, "api/auth/sso/callback")
            .SetQueryParam("code", code)
            .SetQueryParam("state", state)
            .AllowAnyHttpStatus()
            .WithAutoRedirect(false)
            .GetAsync();

        var locationHeader = response.Headers
            .FirstOrDefault(h => h.Name.Equals("Location", StringComparison.OrdinalIgnoreCase))
            .Value;

        var sessionAuthCookie = response.Cookies
            .FirstOrDefault(c => c.Name == CookieName.SessionAuth);

        return new SsoCallbackResult(
            StatusCode: response.StatusCode,
            LocationHeader: locationHeader,
            SessionAuthCookie: sessionAuthCookie != null
                ? new SessionAuthCookie(sessionAuthCookie.Value)
                : null);
    }

    public record SsoInitiateResult(int StatusCode, string? LocationHeader);
    public record SsoCallbackResult(int StatusCode, string? LocationHeader, SessionAuthCookie? SessionAuthCookie);
}
