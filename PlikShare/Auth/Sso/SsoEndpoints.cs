using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Flurl;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using PlikShare.AuthProviders.GetDetails;
using PlikShare.AuthProviders.Id;
using PlikShare.Core.Configuration;
using PlikShare.Core.IdentityProvider;
using PlikShare.Users.Cache;
using PlikShare.Users.Entities;
using Serilog;

namespace PlikShare.Auth.Sso;

public static class SsoEndpoints
{
    private const string CallbackPath = "api/auth/sso/callback";

    public static void MapSsoEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth/sso")
            .WithTags("SSO")
            .AllowAnonymous();

        group.MapGet("/{authProviderExternalId}", InitiateSso)
            .WithName("InitiateSso");

        group.MapGet("/callback", HandleCallback)
            .WithName("SsoCallback");
    }

    private static async Task<IResult> InitiateSso(
        [FromRoute] AuthProviderExtId authProviderExternalId,
        GetAuthProviderDetailsQuery getAuthProviderDetailsQuery,
        OidcDiscoveryCache discoveryCache,
        OidcStateProtector stateProtector,
        IConfig config,
        CancellationToken cancellationToken)
    {
        var provider = getAuthProviderDetailsQuery.Execute(
            authProviderExternalId);

        if (provider is null || !provider.IsActive)
        {
            return RedirectToSignIn(
                config,
                "provider-not-found");
        }

        var discovery = await discoveryCache.GetDocument(
            provider.AutoDiscoveryUrl,
            cancellationToken);

        if (discovery is null)
        {
            return RedirectToSignIn(
                config,
                "provider-unavailable");
        }

        var nonce = Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(32));

        var state = stateProtector.CreateState(
            authProviderExternalId.Value,
            nonce);

        var redirectUri = new Url(config.AppUrl)
            .AppendPathSegment(CallbackPath)
            .ToString();

        var authorizationUrl = new Url(discovery.AuthorizationEndpoint)
            .SetQueryParam("response_type", "code")
            .SetQueryParam("client_id", provider.ClientId)
            .SetQueryParam("redirect_uri", redirectUri)
            .SetQueryParam("scope", "openid email profile")
            .SetQueryParam("state", state)
            .SetQueryParam("nonce", nonce)
            .ToString();

        return Results.Redirect(authorizationUrl);
    }

    private static async Task<IResult> HandleCallback(
        HttpContext httpContext,
        GetAuthProviderDetailsQuery getAuthProviderDetailsQuery,
        OidcDiscoveryCache discoveryCache,
        OidcStateProtector stateProtector,
        GetOrCreateSsoUserQuery getOrCreateSsoUserQuery,
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        UserCache userCache,
        IHttpClientFactory httpClientFactory,
        IConfig config,
        CancellationToken cancellationToken)
    {
        var query = httpContext.Request.Query;

        if (query.ContainsKey("error"))
        {
            var errorDescription = query["error_description"].FirstOrDefault()
                ?? query["error"].FirstOrDefault();

            Log.Warning(
                "SSO IdP returned error: {Error}",
                errorDescription);

            return RedirectToSignIn(
                config,
                "idp-error");
        }

        var stateParam = query["state"].FirstOrDefault();

        if (string.IsNullOrEmpty(stateParam))
        {
            return RedirectToSignIn(
                config,
                "invalid-state");
        }

        var state = stateProtector.ValidateState(stateParam);

        if (state is null)
        {
            return RedirectToSignIn(
                config,
                "invalid-state");
        }

        var code = query["code"].FirstOrDefault();

        if (string.IsNullOrEmpty(code))
        {
            return RedirectToSignIn(
                config,
                "token-exchange-failed");
        }

        var authProviderExternalId = AuthProviderExtId.Parse(
            state.ProviderExternalId);

        var provider = getAuthProviderDetailsQuery.Execute(
            authProviderExternalId);

        if (provider is null || !provider.IsActive)
        {
            return RedirectToSignIn(
                config,
                "provider-not-found");
        }

        var discovery = await discoveryCache.GetDocument(
            provider.AutoDiscoveryUrl,
            cancellationToken);

        if (discovery is null)
        {
            return RedirectToSignIn(
                config,
                "provider-unavailable");
        }

        var redirectUri = new Url(config.AppUrl)
            .AppendPathSegment(CallbackPath)
            .ToString();

        var tokenResponse = await ExchangeCodeForTokens(
            tokenEndpoint: discovery.TokenEndpoint,
            code: code,
            redirectUri: redirectUri,
            clientId: provider.ClientId,
            clientSecret: provider.ClientSecret,
            httpClientFactory: httpClientFactory,
            cancellationToken: cancellationToken);

        if (tokenResponse is null)
        {
            return RedirectToSignIn(
                config,
                "token-exchange-failed");
        }

        var email = ExtractEmailFromIdToken(tokenResponse.IdToken);

        if (string.IsNullOrEmpty(email))
        {
            if (!string.IsNullOrEmpty(discovery.UserinfoEndpoint) &&
                !string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                email = await FetchEmailFromUserinfo(
                    userinfoEndpoint: discovery.UserinfoEndpoint,
                    accessToken: tokenResponse.AccessToken,
                    httpClientFactory: httpClientFactory,
                    cancellationToken: cancellationToken);
            }
        }

        if (string.IsNullOrEmpty(email))
        {
            return RedirectToSignIn(
                config,
                "no-email");
        }

        var sub = ExtractSubFromIdToken(tokenResponse.IdToken);

        if (string.IsNullOrEmpty(sub))
        {
            return RedirectToSignIn(
                config,
                "no-email");
        }

        var userResult = await getOrCreateSsoUserQuery.Execute(
            email: new Email(email),
            loginProvider: authProviderExternalId.Value,
            providerKey: sub,
            providerDisplayName: provider.Name,
            cancellationToken: cancellationToken);

        if (userResult.Code == GetOrCreateSsoUserQuery.ResultCode.RegistrationNotAllowed)
        {
            return RedirectToSignIn(
                config,
                "account-not-found");
        }

        await userCache.InvalidateEntry(
            userId: userResult.User!.Id,
            cancellationToken: cancellationToken);

        var appUser = await userManager.FindByEmailAsync(email);

        if (appUser is null)
        {
            return RedirectToSignIn(
                config,
                "account-not-found");
        }

        await signInManager.SignInAsync(
            user: appUser,
            isPersistent: false);

        Log.Information(
            "User '{UserEmail}' signed in via SSO provider '{ProviderName}'",
            EmailAnonymization.Anonymize(email),
            provider.Name);

        return Results.Redirect(config.AppUrl);
    }

    private static async Task<TokenResponse?> ExchangeCodeForTokens(
        string tokenEndpoint,
        string code,
        string redirectUri,
        string clientId,
        string clientSecret,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = httpClientFactory.CreateClient();

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret
            });

            var response = await client.PostAsync(
                tokenEndpoint,
                content,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(
                    cancellationToken);

                Log.Warning(
                    "Token exchange failed with status {StatusCode}: {ErrorBody}",
                    response.StatusCode,
                    errorBody);

                return null;
            }

            var json = await response.Content.ReadAsStringAsync(
                cancellationToken);

            var raw = JsonSerializer.Deserialize<RawTokenResponse>(json);

            if (raw is null)
            {
                return null;
            }

            return new TokenResponse
            {
                AccessToken = raw.AccessToken,
                IdToken = raw.IdToken
            };
        }
        catch (Exception e)
        {
            Log.Error(
                e,
                "Failed to exchange authorization code for tokens");

            return null;
        }
    }

    private static string? ExtractEmailFromIdToken(string? idToken)
    {
        if (string.IsNullOrEmpty(idToken))
        {
            return null;
        }

        try
        {
            var handler = new JsonWebTokenHandler();
            var jwt = handler.ReadJsonWebToken(idToken);

            return jwt.GetClaim("email")?.Value;
        }
        catch (Exception e)
        {
            Log.Warning(
                e,
                "Failed to extract email from id_token");

            return null;
        }
    }

    private static string? ExtractSubFromIdToken(string? idToken)
    {
        if (string.IsNullOrEmpty(idToken))
        {
            return null;
        }

        try
        {
            var handler = new JsonWebTokenHandler();
            var jwt = handler.ReadJsonWebToken(idToken);

            return jwt.GetClaim("sub")?.Value;
        }
        catch (Exception e)
        {
            Log.Warning(
                e,
                "Failed to extract sub from id_token");

            return null;
        }
    }

    private static async Task<string?> FetchEmailFromUserinfo(
        string userinfoEndpoint,
        string accessToken,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = httpClientFactory.CreateClient();

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.GetAsync(
                userinfoEndpoint,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(
                cancellationToken);

            var userinfo = JsonSerializer.Deserialize<JsonElement>(json);

            return userinfo.TryGetProperty("email", out var emailProp)
                ? emailProp.GetString()
                : null;
        }
        catch (Exception e)
        {
            Log.Warning(
                e,
                "Failed to fetch email from userinfo endpoint");

            return null;
        }
    }

    private static IResult RedirectToSignIn(
        IConfig config,
        string error)
    {
        return Results.Redirect(
            new Url(config.AppUrl)
                .AppendPathSegment("sign-in")
                .SetQueryParam("error", error)
                .ToString());
    }

    private class TokenResponse
    {
        public string? AccessToken { get; init; }
        public string? IdToken { get; init; }
    }

    private class RawTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [JsonPropertyName("id_token")]
        public string? IdToken { get; init; }
    }
}
