using Flurl.Http;
using PlikShare.Integrations.Create.Contracts;
using PlikShare.Integrations.Id;
using PlikShare.Integrations.List.Contracts;
using PlikShare.Integrations.UpdateName.Contracts;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class IntegrationsApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<GetIntegrationsResponseDto> Get(
        SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetIntegrationsResponseDto>(
            appUrl: appUrl,
            apiPath: "api/integrations",
            cookie: cookie);
    }

    public async Task<CreateIntegrationResponseDto> Create(
        CreateIntegrationRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<CreateIntegrationResponseDto, CreateIntegrationRequestDto>(
            appUrl: appUrl,
            apiPath: "api/integrations",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task Delete(
        IntegrationExtId externalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecuteDelete(
            appUrl: appUrl,
            apiPath: $"api/integrations/{externalId}",
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task UpdateName(
        IntegrationExtId externalId,
        UpdateIntegrationNameRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/integrations/{externalId}/name",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task Activate(
        IntegrationExtId externalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePost(
            appUrl: appUrl,
            apiPath: $"api/integrations/{externalId}/activate",
            request: new object(),
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task Deactivate(
        IntegrationExtId externalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePost(
            appUrl: appUrl,
            apiPath: $"api/integrations/{externalId}/deactivate",
            request: new object(),
            cookie: cookie,
            antiforgery: antiforgery);
    }
}
