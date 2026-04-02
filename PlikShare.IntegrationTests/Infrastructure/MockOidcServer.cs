using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace PlikShare.IntegrationTests.Infrastructure;

public class MockOidcServer : IAsyncDisposable
{
    private bool _disposed;
    public int PortNumber { get; }
    public WebApplication App { get; }
    public string IssuerUrl { get; }

    public ConcurrentDictionary<string, AuthCodeConfig> PendingAuthCodes { get; } = new();
    public bool ShouldFailTokenExchange { get; set; }
    public bool ShouldFailClientCredentials { get; set; }

    public MockOidcServer(int portNumber)
    {
        PortNumber = portNumber;
        IssuerUrl = $"https://localhost:{PortNumber}";

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

        App.MapGet("/.well-known/jwks.json", () => TypedResults.Ok(new
        {
            keys = Array.Empty<object>()
        }));

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
                return HandleAuthorizationCode(code);
            }

            return Results.BadRequest(new { error = "unsupported_grant_type" });
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

    private IResult HandleAuthorizationCode(string code)
    {
        if (ShouldFailTokenExchange || !PendingAuthCodes.TryGetValue(code, out var config))
        {
            return Results.BadRequest(new
            {
                error = "invalid_grant",
                error_description = "Authorization code is invalid or expired."
            });
        }

        var idToken = GenerateUnsignedJwt(config.Email, config.Sub);

        return Results.Ok(new
        {
            access_token = $"mock-access-token-{Guid.NewGuid():N}",
            token_type = "Bearer",
            id_token = idToken,
            expires_in = 3600
        });
    }

    private string GenerateUnsignedJwt(string email, string sub)
    {
        var header = new { alg = "none", typ = "JWT" };
        var payload = new
        {
            sub,
            email,
            iss = IssuerUrl,
            aud = "test-client-id",
            iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
            nonce = "test-nonce"
        };

        var headerJson = JsonSerializer.Serialize(header);
        var payloadJson = JsonSerializer.Serialize(payload);

        var headerBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
        var payloadBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));

        return $"{headerBase64}.{payloadBase64}.";
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public void RegisterAuthCode(string code, string email, string sub)
    {
        PendingAuthCodes[code] = new AuthCodeConfig(Email: email, Sub: sub);
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
            await App.StopAsync();
            await App.DisposeAsync();
        }
        finally
        {
            _disposed = true;
        }
    }

    public record AuthCodeConfig(string Email, string Sub);
}
