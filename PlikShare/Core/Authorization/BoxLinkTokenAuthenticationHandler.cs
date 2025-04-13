using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using PlikShare.Core.Clock;
using PlikShare.Core.Utils;

namespace PlikShare.Core.Authorization;

public class BoxLinkTokenAuthenticationHandler(
    BoxLinkTokenService tokenService,
    IOptionsMonitor<BoxLinkTokenAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<BoxLinkTokenAuthenticationOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName.BoxLinkToken, out var boxLinkHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var tokenStr = boxLinkHeader.ToString();

        if (string.IsNullOrEmpty(tokenStr))
        {
            return Task.FromResult(AuthenticateResult.Fail("Empty token"));
        }

        var (success, token) = tokenService.TryExtract(
            tokenStr);

        if (!success)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid token"));
        }

        var principal = new ClaimsPrincipal(
            identity: new ClaimsIdentity(
                [
                    new Claim(Claims.BoxLinkSessionId, token!.SessionId.ToString())
                ],
                AuthScheme.BoxLinkSessionScheme));

        var ticket = new AuthenticationTicket(
            principal,
            AuthScheme.BoxLinkSessionScheme);

        var result = AuthenticateResult.Success(
            ticket);

        return Task.FromResult(result);
    }
}

public class BoxLinkTokenAuthenticationOptions : AuthenticationSchemeOptions;

public class BoxLinkTokenService(
    IClock clock,
    IDataProtectionProvider dataProtectionProvider)
{
    private const string BoxLinkTokenPurpose = "BoxLinkToken";

    public string Generate()
    {
        var token = new BoxLinkToken(
            SessionId: Guid.NewGuid(),
            CreatedAt: clock.UtcNow);

        var serialized = Json.Serialize(token);

        var protector = dataProtectionProvider.CreateProtector(
            BoxLinkTokenPurpose);

        var protectedData = protector.Protect(
            serialized);

        var urlEncoded = HttpUtility.UrlEncode(
            protectedData);

        return urlEncoded;
    }

    public (bool Success, BoxLinkToken? Token) TryExtract(
        string protectedDataUrlEncoded)
    {
        try
        {
            var protector = dataProtectionProvider.CreateProtector(
                BoxLinkTokenPurpose);

            var protectedData = HttpUtility.UrlDecode(
                protectedDataUrlEncoded);

            var jsonParameters = protector.Unprotect(
                protectedData);

            var token = Json.Deserialize<BoxLinkToken>(
                jsonParameters);

            if (token is null)
                return (false, null);
            
            return (true, token);
        }
        catch (Exception)
        {
            return (false, null);
        }
    }
}

public record BoxLinkToken(
    Guid SessionId,
    DateTimeOffset CreatedAt);
