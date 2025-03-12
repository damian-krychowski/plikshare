using System.Diagnostics;
using Flurl.Http;
using PlikShare.Auth.Contracts;
using PlikShare.Core.Authorization;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class AuthApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<(SignInUserResponseDto, SessionAuthCookie?, TwoFactorUserIdCookie?)> SignIn(
        string email,
        string password,
        TwoFactorRememberMeCookie? twoFactorRememberMeCookie = null)
    {
        var request = flurlClient
            .Request(appUrl, "api/auth/sign-in")
            .AllowAnyHttpStatus();

        if (twoFactorRememberMeCookie is not null)
        {
            request = request.WithCookie(
                name: twoFactorRememberMeCookie.Name,
                value: twoFactorRememberMeCookie.Value);
        }

        var response = await request.PostJsonAsync(new SignInUserRequestDto(
            Email: email,
            Password: password,
            RememberMe: false));

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            var exception = new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode);

            throw exception;
        }

        var responseBody = await response
            .GetJsonAsync<SignInUserResponseDto>();

        if (responseBody == SignInUserResponseDto.Successful)
        {
            var sessionAuthCookie = response
                .Cookies
                .FirstOrDefault(c => c.Name == CookieName.SessionAuth);

            Debug.Assert(sessionAuthCookie != null);
            
            return (responseBody, new SessionAuthCookie(sessionAuthCookie.Value), null);
        }

        if (responseBody == SignInUserResponseDto.Required2Fa)
        {
            var twoFactorCookie = response
                .Cookies
                .FirstOrDefault(c => c.Name == CookieName.TwoFactorUserId);

            Debug.Assert(twoFactorCookie != null);

            return (responseBody, null, new TwoFactorUserIdCookie(twoFactorCookie.Value));
        }

        return (responseBody, null, null);
    }

    public async Task<(SignInUser2FaResponseDto, SessionAuthCookie?, TwoFactorRememberMeCookie?)> SignIn2Fa(
        SignInUser2FaRequestDto request,
        TwoFactorUserIdCookie cookie)
    {
        var response = await flurlClient
            .Request(appUrl, "api/auth/sign-in-2fa")
            .AllowAnyHttpStatus()
            .WithCookie(cookie.Name, cookie.Value)
            .PostJsonAsync(request);

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            var exception = new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode);

            throw exception;
        }

        var responseBody = await response
            .GetJsonAsync<SignInUser2FaResponseDto>();

        if (responseBody == SignInUser2FaResponseDto.Successful)
        {
            var sessionAuthCookie = response
                .Cookies
                .FirstOrDefault(c => c.Name == CookieName.SessionAuth);

            Debug.Assert(sessionAuthCookie != null);

            var twoFactorRememberMeCookie = response
                .Cookies
                .FirstOrDefault(c => c.Name == CookieName.TwoFactorRememberMe);

            return (
                responseBody, 
                new SessionAuthCookie(sessionAuthCookie.Value),
                twoFactorRememberMeCookie is not null 
                    ? new TwoFactorRememberMeCookie(twoFactorRememberMeCookie.Value) 
                    : null);
        }

        return (responseBody, null, null);
    }

    public async Task<(SignInUserRecoveryCodeResponseDto, SessionAuthCookie?)> SignInRecoveryCode(
        SignInUserRecoveryCodeRequestDto request,
        TwoFactorUserIdCookie cookie)
    {
        var response = await flurlClient
            .Request(appUrl, "api/auth/sign-in-recovery-code")
            .AllowAnyHttpStatus()
            .WithCookie(cookie.Name, cookie.Value)
            .PostJsonAsync(request);

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            var exception = new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode);

            throw exception;
        }

        var responseBody = await response
            .GetJsonAsync<SignInUserRecoveryCodeResponseDto>();

        if (responseBody == SignInUserRecoveryCodeResponseDto.Successful)
        {
            var sessionAuthCookie = response
                .Cookies
                .FirstOrDefault(c => c.Name == CookieName.SessionAuth);

            Debug.Assert(sessionAuthCookie != null);

            return (responseBody, new SessionAuthCookie(sessionAuthCookie.Value));
        }

        return (responseBody, null);
    }

    public async Task<SessionAuthCookie> SignInOrThrow(User user)
    {
        var response = await flurlClient
            .Request(appUrl, "api/auth/sign-in")
            .AllowAnyHttpStatus()
            .PostJsonAsync(new SignInUserRequestDto(
                Email: user.Email,
                Password: user.Password,
                RememberMe: false));

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            var exception = new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode);

            throw exception;
        }

        var responseBody = await response
            .GetJsonAsync<SignInUserResponseDto>();

        if (responseBody != SignInUserResponseDto.Successful)
            throw new InvalidOperationException($"Sign in for user should work, but instead '{responseBody.Code}' was received");
            
        var sessionAuthCookie = response
            .Cookies
            .FirstOrDefault(c => c.Name == CookieName.SessionAuth);

        if(sessionAuthCookie is null)
            throw new InvalidOperationException($"Sign in for user should worked, but '{CookieName.SessionAuth}' was not found");
            
        return new SessionAuthCookie(sessionAuthCookie.Value);
    }

    public async Task<(SignUpUserResponseDto, SessionAuthCookie?)> SignUp(
        SignUpUserRequestDto request)
    {
        var response = await flurlClient
            .Request(appUrl, "api/auth/sign-up")
            .AllowAnyHttpStatus()
            .PostJsonAsync(request);

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            var exception = new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode);

            throw exception;
        }

        var responseBody = await response
            .GetJsonAsync<SignUpUserResponseDto>();

        if (responseBody.Code == SignUpUserResponseDto.SingedUpAndSignedIn.Code)
        {
            var sessionAuthCookie = response
                .Cookies
                .FirstOrDefault(c => c.Name == CookieName.SessionAuth);

            Debug.Assert(sessionAuthCookie != null);

            return (responseBody, new SessionAuthCookie(sessionAuthCookie.Value));
        }

        return (responseBody, null);
    }
}