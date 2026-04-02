using System.Text.Json;
using System.Text.Json.Serialization;
using PlikShare.AuthProviders.Entities;
using Serilog;

namespace PlikShare.AuthProviders.TestConfiguration;

public class TestAuthProviderConfigurationOperation(
    IHttpClientFactory httpClientFactory)
{
    public async Task<Result> Execute(
        string issuerUrl,
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken)
    {
        var discoveryUrl = OidcUrls.GetDiscoveryUrl(issuerUrl);

        try
        {
            using var client = httpClientFactory.CreateClient();

            var discoveryResponse = await client.GetAsync(
                discoveryUrl,
                cancellationToken);

            if (!discoveryResponse.IsSuccessStatusCode)
            {
                return new Result(
                    Code: ResultCode.DiscoveryFailed,
                    Details: $"Discovery endpoint returned {discoveryResponse.StatusCode}");
            }

            var discoveryJson = await discoveryResponse.Content.ReadAsStringAsync(
                cancellationToken);

            var discovery = JsonSerializer.Deserialize<RawDiscoveryDocument>(discoveryJson);

            if (discovery?.TokenEndpoint is null || discovery.AuthorizationEndpoint is null)
            {
                return new Result(
                    Code: ResultCode.DiscoveryFailed,
                    Details: "Discovery document is missing required fields (token_endpoint, authorization_endpoint)");
            }

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret
            });

            var tokenResponse = await client.PostAsync(
                discovery.TokenEndpoint,
                content,
                cancellationToken);

            var tokenJson = await tokenResponse.Content.ReadAsStringAsync(
                cancellationToken);

            if (tokenResponse.IsSuccessStatusCode)
            {
                return new Result(
                    Code: ResultCode.Ok,
                    Details: "Configuration is valid. Discovery document fetched and client credentials verified.");
            }

            var errorResponse = JsonSerializer.Deserialize<TokenErrorResponse>(tokenJson);

            if (errorResponse?.Error is "unauthorized_client" or "unsupported_grant_type")
            {
                return new Result(
                    Code: ResultCode.Ok,
                    Details: "Configuration is valid. Client credentials verified (client_credentials grant is not enabled, which is expected).");
            }

            if (errorResponse?.Error == "invalid_client")
            {
                return new Result(
                    Code: ResultCode.InvalidCredentials,
                    Details: "Client ID or Client Secret is incorrect.");
            }

            return new Result(
                Code: ResultCode.InvalidCredentials,
                Details: $"Token endpoint returned error: {errorResponse?.Error ?? "unknown"} - {errorResponse?.ErrorDescription ?? tokenJson}");
        }
        catch (Exception e)
        {
            Log.Warning(
                e,
                "Failed to test auth provider configuration for '{IssuerUrl}'",
                issuerUrl);

            return new Result(
                Code: ResultCode.DiscoveryFailed,
                Details: $"Could not reach identity provider: {e.Message}");
        }
    }

    public enum ResultCode
    {
        Ok,
        DiscoveryFailed,
        InvalidCredentials
    }

    public record Result(
        ResultCode Code,
        string? Details = null);

    private class RawDiscoveryDocument
    {
        [JsonPropertyName("authorization_endpoint")]
        public string? AuthorizationEndpoint { get; init; }

        [JsonPropertyName("token_endpoint")]
        public string? TokenEndpoint { get; init; }
    }

    private class TokenErrorResponse
    {
        [JsonPropertyName("error")]
        public string? Error { get; init; }

        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; init; }
    }
}
