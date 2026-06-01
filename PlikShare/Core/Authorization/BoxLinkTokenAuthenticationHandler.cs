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
    /// <summary>
    /// Query-string parameter name accepted as a fallback for the <c>X-BOX-LINK-TOKEN</c> header.
    /// Browsers don't send custom headers on <c>&lt;img src&gt;</c> / <c>&lt;a href&gt;</c> /
    /// pre-signed-link requests, so URL-embedded auth is the only way to keep those flows working
    /// without re-architecting to cookies. Query takes precedence after header is checked.
    /// </summary>
    public const string QueryParameterName = "boxLinkToken";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? tokenStr = null;

        if (Request.Headers.TryGetValue(HeaderName.BoxLinkToken, out var boxLinkHeader))
        {
            tokenStr = boxLinkHeader.ToString();
        }
        else if (Request.Query.TryGetValue(QueryParameterName, out var queryToken))
        {
            tokenStr = queryToken.ToString();
        }

        if (tokenStr is null)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

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
                    new Claim(Claims.BoxLinkSessionIdClaim, token!.SessionId.ToString())
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
