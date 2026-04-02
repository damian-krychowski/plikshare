using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace PlikShare.Auth.Sso;

public class OidcJwksCache(
    HybridCache cache,
    IHttpClientFactory httpClientFactory)
{
    private static string JwksKey(string jwksUri) => $"oidc:jwks:{jwksUri}";

    public async ValueTask<IList<SecurityKey>?> GetSigningKeys(
        string jwksUri,
        CancellationToken cancellationToken)
    {
        var json = await cache.GetOrCreateAsync(
            key: JwksKey(jwksUri),
            factory: async ct => await FetchJwksJson(jwksUri, ct),
            cancellationToken: cancellationToken);

        if (json is null)
            return null;

        var jwks = new JsonWebKeySet(json);
        return jwks.GetSigningKeys();
    }

    private async Task<string?> FetchJwksJson(
        string jwksUri,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = httpClientFactory.CreateClient();

            var response = await client.GetAsync(
                jwksUri,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync(
                cancellationToken);
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to fetch JWKS from '{JwksUri}'", jwksUri);
            return null;
        }
    }

    public ValueTask InvalidateCache(
        string jwksUri,
        CancellationToken cancellationToken)
    {
        return cache.RemoveAsync(
            JwksKey(jwksUri),
            cancellationToken);
    }
}
