using Flurl.Http;
using PlikShare.EmailProviders.Confirm.Contracts;
using PlikShare.EmailProviders.ExternalProviders.Resend.Create;
using PlikShare.EmailProviders.Id;
using PlikShare.EmailProviders.List.Contracts;
using PlikShare.EmailProviders.UpdateName.Contracts;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class EmailProvidersApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<GetEmailProvidersResponseDto> Get(
        SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetEmailProvidersResponseDto>(
            appUrl: appUrl,
            apiPath: "api/email-providers",
            cookie: cookie);
    }

    public async Task<CreateResendEmailProviderResponseDto> CreateResend(
        CreateResendEmailProviderRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<CreateResendEmailProviderResponseDto, CreateResendEmailProviderRequestDto>(
            appUrl: appUrl,
            apiPath: "api/email-providers/resend",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }
    
    public async Task Confirm(
        EmailProviderExtId emailProviderExternalId,
        ConfirmEmailProviderRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePost(
            appUrl: appUrl,
            apiPath: $"api/email-providers/{emailProviderExternalId}/confirm",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }
    
    public async Task Activate(
        EmailProviderExtId emailProviderExternalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePost(
            appUrl: appUrl,
            apiPath: $"api/email-providers/{emailProviderExternalId}/activate",
            request: new object(),
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task Deactivate(
        EmailProviderExtId emailProviderExternalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePost(
            appUrl: appUrl,
            apiPath: $"api/email-providers/{emailProviderExternalId}/deactivate",
            request: new object(),
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task Delete(
        EmailProviderExtId emailProviderExternalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecuteDelete(
            appUrl: appUrl,
            apiPath: $"api/email-providers/{emailProviderExternalId}",
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task UpdateName(
        EmailProviderExtId emailProviderExternalId,
        UpdateEmailProviderNameRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/email-providers/{emailProviderExternalId}/name",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }
}