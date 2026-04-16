using System.Diagnostics;
using Flurl.Http;
using PlikShare.Core.Encryption;
using PlikShare.Users;
using PlikShare.Users.Id;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class UserEncryptionPasswordApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<SetupResult> Setup(
        UserExtId userExternalId,
        string encryptionPassword,
        SessionAuthCookie cookie,
        AntiforgeryCookies antiforgery)
    {
        var response = await flurlClient
            .Request(appUrl, "api/user-encryption-password/setup")
            .AllowAnyHttpStatus()
            .WithCookie(cookie)
            .WithAntiforgery(antiforgery)
            .PostJsonAsync(new SetupRequestDto(EncryptionPassword: encryptionPassword));

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            throw new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode);
        }

        var body = await response.GetJsonAsync<SetupResponseDto>();

        var cookieName = UserEncryptionSessionCookie.GetCookieName(userExternalId);
        var encryptionCookie = response
            .Cookies
            .FirstOrDefault(c => c.Name == cookieName);

        Debug.Assert(encryptionCookie != null,
            $"Set-Cookie '{cookieName}' was not present in setup response.");

        return new SetupResult(
            RecoveryCode: body.RecoveryCode,
            EncryptionCookie: new GenericCookie(cookieName, encryptionCookie!.Value));
    }

    public async Task<GenericCookie> Unlock(
        UserExtId userExternalId,
        string encryptionPassword,
        SessionAuthCookie cookie,
        AntiforgeryCookies antiforgery)
    {
        var response = await flurlClient
            .Request(appUrl, "api/user-encryption-password/unlock")
            .AllowAnyHttpStatus()
            .WithCookie(cookie)
            .WithAntiforgery(antiforgery)
            .PostJsonAsync(new UnlockRequestDto(EncryptionPassword: encryptionPassword));

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            throw new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode);
        }

        var cookieName = UserEncryptionSessionCookie.GetCookieName(userExternalId);
        var encryptionCookie = response
            .Cookies
            .FirstOrDefault(c => c.Name == cookieName);

        Debug.Assert(encryptionCookie != null,
            $"Set-Cookie '{cookieName}' was not present in unlock response.");

        return new GenericCookie(cookieName, encryptionCookie!.Value);
    }

    public async Task Lock(
        SessionAuthCookie cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePost(
            appUrl: appUrl,
            apiPath: "api/user-encryption-password/lock",
            request: new { },
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task<GenericCookie> Change(
        UserExtId userExternalId,
        string oldPassword,
        string newPassword,
        SessionAuthCookie cookie,
        AntiforgeryCookies antiforgery)
    {
        var response = await flurlClient
            .Request(appUrl, "api/user-encryption-password/change")
            .AllowAnyHttpStatus()
            .WithCookie(cookie)
            .WithAntiforgery(antiforgery)
            .PostJsonAsync(new ChangeRequestDto(OldPassword: oldPassword, NewPassword: newPassword));

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            throw new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode);
        }

        var cookieName = UserEncryptionSessionCookie.GetCookieName(userExternalId);
        var encryptionCookie = response
            .Cookies
            .FirstOrDefault(c => c.Name == cookieName);

        Debug.Assert(encryptionCookie != null,
            $"Set-Cookie '{cookieName}' was not present in change response.");

        return new GenericCookie(cookieName, encryptionCookie!.Value);
    }

    public async Task<GenericCookie> Reset(
        UserExtId userExternalId,
        string recoveryCode,
        string newPassword,
        SessionAuthCookie cookie,
        AntiforgeryCookies antiforgery)
    {
        var response = await flurlClient
            .Request(appUrl, "api/user-encryption-password/reset")
            .AllowAnyHttpStatus()
            .WithCookie(cookie)
            .WithAntiforgery(antiforgery)
            .PostJsonAsync(new ResetRequestDto(RecoveryCode: recoveryCode, NewPassword: newPassword));

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            throw new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode);
        }

        var cookieName = UserEncryptionSessionCookie.GetCookieName(userExternalId);
        var encryptionCookie = response
            .Cookies
            .FirstOrDefault(c => c.Name == cookieName);

        Debug.Assert(encryptionCookie != null,
            $"Set-Cookie '{cookieName}' was not present in reset response.");

        return new GenericCookie(cookieName, encryptionCookie!.Value);
    }

    public record SetupResult(string RecoveryCode, GenericCookie EncryptionCookie);
}
