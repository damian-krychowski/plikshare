using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using PlikShare.Core.Authorization;

namespace PlikShare.Agents.Authorization;

public class AgentTokenAuthenticationHandler(
    AgentTokenVerifier agentTokenVerifier,
    IOptionsMonitor<AgentTokenAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AgentTokenAuthenticationOptions>(options, logger, encoder)
{
    private const string BearerPrefix = "Bearer ";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
            return Task.FromResult(AuthenticateResult.NoResult());

        var headerValue = authorizationHeader.ToString();

        if (string.IsNullOrEmpty(headerValue) || !headerValue.StartsWith(BearerPrefix, StringComparison.Ordinal))
            return Task.FromResult(AuthenticateResult.NoResult());

        var token = headerValue[BearerPrefix.Length..].Trim();

        if (string.IsNullOrEmpty(token) || !token.StartsWith(AgentTokenService.Prefix, StringComparison.Ordinal))
            return Task.FromResult(AuthenticateResult.NoResult());

        var verifiedAgent = agentTokenVerifier.TryVerify(
            token: token);

        if (verifiedAgent is null)
            return Task.FromResult(AuthenticateResult.Fail("Invalid agent token"));

        var principal = new ClaimsPrincipal(
            identity: new ClaimsIdentity(
                [
                    new Claim(Claims.AgentExternalIdClaim, verifiedAgent.ExternalId.Value)
                ],
                AuthScheme.AgentTokenScheme));

        var ticket = new AuthenticationTicket(
            principal,
            AuthScheme.AgentTokenScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public class AgentTokenAuthenticationOptions : AuthenticationSchemeOptions;
