using Flurl.Http;
using PlikShare.AuthProviders.Create.Contracts;
using PlikShare.AuthProviders.Id;
using PlikShare.AuthProviders.List.Contracts;
using PlikShare.AuthProviders.PasswordLogin.Contracts;
using PlikShare.AuthProviders.TestConfiguration.Contracts;
using PlikShare.AuthProviders.Update.Contracts;
using PlikShare.AuthProviders.UpdateName.Contracts;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class AuthProvidersApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<GetAuthSettingsResponseDto> Get(
        SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetAuthSettingsResponseDto>(
            appUrl: appUrl,
            apiPath: "api/auth-providers",
            cookie: cookie);
    }

    public async Task<CreateOidcAuthProviderResponseDto> CreateOidc(
        CreateOidcAuthProviderRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<CreateOidcAuthProviderResponseDto, CreateOidcAuthProviderRequestDto>(
            appUrl: appUrl,
            apiPath: "api/auth-providers/oidc",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task Update(
        AuthProviderExtId externalId,
        UpdateAuthProviderRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePut(
            appUrl: appUrl,
            apiPath: $"api/auth-providers/{externalId}",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task UpdateName(
        AuthProviderExtId externalId,
        UpdateAuthProviderNameRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/auth-providers/{externalId}/name",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task Activate(
        AuthProviderExtId externalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePost(
            appUrl: appUrl,
            apiPath: $"api/auth-providers/{externalId}/activate",
            request: new object(),
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task Deactivate(
        AuthProviderExtId externalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePost(
            appUrl: appUrl,
            apiPath: $"api/auth-providers/{externalId}/deactivate",
            request: new object(),
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task Delete(
        AuthProviderExtId externalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecuteDelete(
            appUrl: appUrl,
            apiPath: $"api/auth-providers/{externalId}",
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task SetPasswordLogin(
        SetPasswordLoginRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePut(
            appUrl: appUrl,
            apiPath: "api/auth-providers/password-login-enabled",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task<TestAuthProviderConfigurationResponseDto> TestConfiguration(
        TestAuthProviderConfigurationRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<TestAuthProviderConfigurationResponseDto, TestAuthProviderConfigurationRequestDto>(
            appUrl: appUrl,
            apiPath: "api/auth-providers/test-configuration",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }
}
