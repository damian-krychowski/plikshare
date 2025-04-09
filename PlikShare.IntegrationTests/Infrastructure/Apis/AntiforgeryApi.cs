using System.Diagnostics;
using Flurl.Http;
using PlikShare.Core.Authorization;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class AntiforgeryApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<AntiforgeryCookies> GetToken(
        SessionAuthCookie? cookie = null)
    {
        var response = await flurlClient
            .Request(appUrl, "api/antiforgery/token")
            .AllowAnyHttpStatus()
            .WithCookie(cookie)
            .GetAsync();

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            var exception = new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode);

            throw exception;
        }

        return ExtractAntiforgeryCookies(
            response,
            CookieName.Antiforgery);
    }
    
    private static AntiforgeryCookies ExtractAntiforgeryCookies(
        IFlurlResponse response,
        string antiforgeryCookieName)
    {
        var antiforgeryTokenCookie = response
            .Cookies
            .FirstOrDefault(c => c.Name == antiforgeryCookieName);

        Debug.Assert(antiforgeryTokenCookie != null);

        var aspNetAntiforgeryCookie = response
            .Cookies
            .FirstOrDefault(c => c.Name.StartsWith(".AspNetCore.Antiforgery", StringComparison.InvariantCultureIgnoreCase));


        Debug.Assert(aspNetAntiforgeryCookie != null);

        return new AntiforgeryCookies
        {
            AntiforgeryToken = new GenericCookie(
                antiforgeryTokenCookie.Name,
                antiforgeryTokenCookie.Value),

            AspNetAntiforgery = new GenericCookie(
                aspNetAntiforgeryCookie.Name,
                aspNetAntiforgeryCookie.Value)
        };
    }
}

public class AntiforgeryCookies
{
    public required Cookie AntiforgeryToken { get; init; }
    public required Cookie AspNetAntiforgery { get; init; }
}
