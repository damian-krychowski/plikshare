using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Hybrid;
using Serilog;

namespace PlikShare.Auth.Sso;

public class OidcDiscoveryCache(
    HybridCache cache,
    IHttpClientFactory httpClientFactory)
{
    private static string DiscoveryKey(string discoveryUrl) => $"oidc:discovery:{discoveryUrl}";

    public async ValueTask<OidcDiscoveryDocument?> GetDocument(
        string discoveryUrl,
        CancellationToken cancellationToken)
    {
        var cached = await cache.GetOrCreateAsync(
            key: DiscoveryKey(discoveryUrl),
            factory: async ct => await FetchDocument(discoveryUrl, ct),
            cancellationToken: cancellationToken);

        return cached;
    }

    private async Task<OidcDiscoveryDocument?> FetchDocument(
        string discoveryUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = httpClientFactory
                .CreateClient();

            var response = await client.GetAsync(
                discoveryUrl, 
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(
                cancellationToken);

            var raw = JsonSerializer.Deserialize<RawDiscoveryDocument>(
                json);

            if (raw is null)
                return null;

            return new OidcDiscoveryDocument
            {
                AuthorizationEndpoint = raw.AuthorizationEndpoint,
                TokenEndpoint = raw.TokenEndpoint,
                UserinfoEndpoint = raw.UserinfoEndpoint,
                JwksUri = raw.JwksUri,
                Issuer = raw.Issuer
            };
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to fetch OIDC discovery document from '{DiscoveryUrl}'", discoveryUrl);
            return null;
        }
    }

    public ValueTask InvalidateCache(
        string discoveryUrl,
        CancellationToken cancellationToken)
    {
        return cache.RemoveAsync(
            DiscoveryKey(discoveryUrl),
            cancellationToken);
    }

    private class RawDiscoveryDocument
    {
        [JsonPropertyName("authorization_endpoint")]
        public required string AuthorizationEndpoint { get; init; }

        [JsonPropertyName("token_endpoint")]
        public required string TokenEndpoint { get; init; }

        [JsonPropertyName("userinfo_endpoint")]
        public string? UserinfoEndpoint { get; init; }

        [JsonPropertyName("jwks_uri")]
        public required string JwksUri { get; init; }

        [JsonPropertyName("issuer")]
        public required string Issuer { get; init; }
    }
}
