using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace PlikShare.IntegrationTests.Infrastructure;

public class MockOidcServer : IAsyncDisposable
{
    private bool _disposed;
    public int PortNumber { get; }
    public WebApplication App { get; }
    public string IssuerUrl { get; }

    private readonly RSA _rsaKey;
    private readonly RSA _untrustedRsaKey;
    private readonly RsaSecurityKey _signingKey;
    private readonly RsaSecurityKey _untrustedSigningKey;
    private const string Kid = "mock-key-1";

    public ConcurrentDictionary<string, AuthCodeConfig> PendingAuthCodes { get; } = new();
    public bool ShouldFailTokenExchange { get; set; }
    public bool ShouldFailClientCredentials { get; set; }

    public MockOidcServer(int portNumber)
    {
        PortNumber = portNumber;
        IssuerUrl = $"https://localhost:{PortNumber}";

        _rsaKey = RSA.Create(2048);
        _signingKey = new RsaSecurityKey(_rsaKey) { KeyId = Kid };

        _untrustedRsaKey = RSA.Create(2048);
        _untrustedSigningKey = new RsaSecurityKey(_untrustedRsaKey) { KeyId = "untrusted-key" };

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(IssuerUrl);

        App = builder.Build();

        App.MapGet("/.well-known/openid-configuration", () => TypedResults.Ok(new
        {
            issuer = IssuerUrl,
            authorization_endpoint = $"{IssuerUrl}/authorize",
            token_endpoint = $"{IssuerUrl}/token",
            userinfo_endpoint = $"{IssuerUrl}/userinfo",
            jwks_uri = $"{IssuerUrl}/.well-known/jwks.json"
        }));

        App.MapGet("/.well-known/jwks.json", () =>
        {
            var parameters = _rsaKey.ExportParameters(
                includePrivateParameters: false);

            return Results.Ok(new
            {
                keys = new[]
                {
                    new
                    {
                        kty = "RSA",
                        kid = Kid,
                        use = "sig",
                        alg = "RS256",
                        n = Base64UrlEncoder.Encode(parameters.Modulus!),
                        e = Base64UrlEncoder.Encode(parameters.Exponent!)
                    }
                }
            });
        });

        App.MapPost("/token", async (HttpContext context) =>
        {
            var form = await context.Request.ReadFormAsync();
            var grantType = form["grant_type"].ToString();

            if (grantType == "client_credentials")
            {
                return HandleClientCredentials();
            }

            if (grantType == "authorization_code")
            {
                var code = form["code"].ToString();
                var codeVerifier = form["code_verifier"].ToString();
                return HandleAuthorizationCode(code, codeVerifier);
            }

            return Results.BadRequest(new
            {
                error = "unsupported_grant_type"
            });
        });

        App.MapGet("/userinfo", (HttpContext context) =>
        {
            return Results.Ok(new
            {
                email = "default@sso.test",
                sub = "default-sub"
            });
        });

        App.Start();
    }

    private IResult HandleClientCredentials()
    {
        if (ShouldFailClientCredentials)
        {
            return Results.BadRequest(new
            {
                error = "invalid_client",
                error_description = "Client ID or Client Secret is incorrect."
            });
        }

        return Results.BadRequest(new
        {
            error = "unauthorized_client",
            error_description = "Client credentials grant is not enabled."
        });
    }

    private IResult HandleAuthorizationCode(string code, string codeVerifier)
    {
        if (ShouldFailTokenExchange || !PendingAuthCodes.TryGetValue(code, out var config))
        {
            return Results.BadRequest(new
            {
                error = "invalid_grant",
                error_description = "Authorization code is invalid or expired."
            });
        }

        if (!ValidateCodeVerifier(codeVerifier, config.CodeChallenge))
        {
            return Results.BadRequest(new
            {
                error = "invalid_grant",
                error_description = "PKCE code_verifier validation failed."
            });
        }

        var signingKey = config.TokenOverrides?.UseUntrustedSigningKey == true
            ? _untrustedSigningKey
            : _signingKey;

        var issuer = config.TokenOverrides?.Issuer ?? IssuerUrl;
        var audience = config.TokenOverrides?.Audience ?? config.ClientId;
        var nonce = config.TokenOverrides?.Nonce ?? config.Nonce;
        var expires = config.TokenOverrides?.Expires ?? DateTime.UtcNow.AddHours(1);

        var idToken = GenerateSignedJwt(
            config.Email, config.Sub, nonce, audience, issuer, expires, signingKey);

        return Results.Ok(new
        {
            access_token = $"mock-access-token-{Guid.NewGuid():N}",
            token_type = "Bearer",
            id_token = idToken,
            expires_in = 3600
        });
    }

    private string GenerateSignedJwt(
        string email, string sub, string nonce,
        string audience, string issuer, DateTime expires,
        RsaSecurityKey key)
    {
        var handler = new JsonWebTokenHandler();

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = sub,
                ["email"] = email,
                ["nonce"] = nonce
            },
            IssuedAt = DateTime.UtcNow,
            Expires = expires,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256)
        };

        return handler.CreateToken(descriptor);
    }

    public void RegisterAuthCode(
        string code, string email, string sub,
        string nonce, string clientId, string codeChallenge,
        TokenOverrides? tokenOverrides = null)
    {
        PendingAuthCodes[code] = new AuthCodeConfig(
            Email: email,
            Sub: sub,
            Nonce: nonce,
            ClientId: clientId,
            CodeChallenge: codeChallenge,
            TokenOverrides: tokenOverrides);
    }

    public void Reset()
    {
        PendingAuthCodes.Clear();
        ShouldFailTokenExchange = false;
        ShouldFailClientCredentials = false;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        try
        {
            _rsaKey.Dispose();
            _untrustedRsaKey.Dispose();
            await App.StopAsync();
            await App.DisposeAsync();
        }
        finally
        {
            _disposed = true;
        }
    }

    private static bool ValidateCodeVerifier(string codeVerifier, string expectedCodeChallenge)
    {
        if (string.IsNullOrEmpty(codeVerifier) || string.IsNullOrEmpty(expectedCodeChallenge))
            return false;

        var challengeBytes = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        var actualChallenge = Base64Url.EncodeToString(challengeBytes);

        return actualChallenge == expectedCodeChallenge;
    }

    public record AuthCodeConfig(
        string Email, string Sub, string Nonce,
        string ClientId, string CodeChallenge,
        TokenOverrides? TokenOverrides = null);

    public record TokenOverrides
    {
        public bool UseUntrustedSigningKey { get; init; }
        public string? Issuer { get; init; }
        public string? Audience { get; init; }
        public string? Nonce { get; init; }
        public DateTime? Expires { get; init; }
    }
}
