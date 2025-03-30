using System.Diagnostics;
using Flurl.Http;
using PlikShare.Account.Contracts;
using PlikShare.Core.Authorization;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class AccountApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<GetAccountDetailsResponseDto> GetDetails(SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetAccountDetailsResponseDto>(
            appUrl: appUrl,
            apiPath: "api/account/details",
            cookie: cookie);
    }

    public async Task<(Get2FaStatusResponseDto, SessionAuthCookie? cookie)> Get2FaStatus(
        SessionAuthCookie? cookie)
    {
        var request = flurlClient
            .Request(appUrl, "api/account/2fa/status")
            .AllowAnyHttpStatus();

        if (cookie is not null)
        {
            request = request.WithCookie(cookie.Name, cookie.Value);
        }

        var response = await request.GetAsync();

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            var exception = new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode);

            throw exception;
        }

        var responseBody = await response
            .GetJsonAsync<Get2FaStatusResponseDto>();

        //it means that this code was regenerated and thus security stamp of user has changed
        if (responseBody.QrCodeUri is not null)
        {
            var sessionAuthCookie = response
                .Cookies
                .FirstOrDefault(c => c.Name == CookieName.SessionAuth);

            Debug.Assert(sessionAuthCookie != null);

            return (responseBody, new SessionAuthCookie(sessionAuthCookie.Value));
        }

        return (responseBody, null);
    }

    public async Task<(Enable2FaResponseDto, SessionAuthCookie? cookie)> Enable2Fa(
        Enable2FaRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgeryCookies)
    {
        var flurlRequest = flurlClient
            .Request(appUrl, "api/account/2fa/enable")
            .AllowAnyHttpStatus()
            .WithAntiforgery(antiforgeryCookies);

        if (cookie is not null)
        {
            flurlRequest = flurlRequest.WithCookie(cookie.Name, cookie.Value);
        }
        
        var response = await flurlRequest.PostJsonAsync(request);

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            var exception = new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode);

            throw exception;
        }

        var responseBody = await response
            .GetJsonAsync<Enable2FaResponseDto>();

        if (responseBody.Code == Enable2FaResponseDto.EnabledCode)
        {
            var sessionAuthCookie = response
                .Cookies
                .FirstOrDefault(c => c.Name == CookieName.SessionAuth);

            Debug.Assert(sessionAuthCookie != null);

            return (responseBody, new SessionAuthCookie(sessionAuthCookie.Value));
        }

        return (responseBody, null);
    }

    public async Task<(Disable2FaResponseDto, SessionAuthCookie? cookie)> Disable2Fa(
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgeryCookies)
    {
        var flurlRequest = flurlClient
            .Request(appUrl, "api/account/2fa/disable")
            .AllowAnyHttpStatus()
            .WithAntiforgery(antiforgeryCookies);

        if (cookie is not null)
        {
            flurlRequest = flurlRequest.WithCookie(cookie.Name, cookie.Value);
        }

        var response = await flurlRequest.PostAsync();

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            var exception = new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode);

            throw exception;
        }

        var responseBody = await response
            .GetJsonAsync<Disable2FaResponseDto>();

        if (responseBody.Code == Disable2FaResponseDto.Disabled.Code)
        {
            var sessionAuthCookie = response
                .Cookies
                .FirstOrDefault(c => c.Name == CookieName.SessionAuth);

            Debug.Assert(sessionAuthCookie != null);

            return (responseBody, new SessionAuthCookie(sessionAuthCookie.Value));
        }

        return (responseBody, null);
    }

    public async Task<GenerateRecoveryCodesResponseDto> GenerateRecoveryCode(
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgeryCookies)
    {
        return await flurlClient.ExecutePost<GenerateRecoveryCodesResponseDto, object>(
            appUrl: appUrl,
            apiPath: "api/account/2fa/generate-recovery-codes",
            request: new object(),
            cookie: cookie,
            antiforgery: antiforgeryCookies);
    }
}