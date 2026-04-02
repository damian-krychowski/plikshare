using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace PlikShare.Auth.Sso;

public static class OidcUtils
{
    private const int CodeVerifierByteLength = 32;

    public static string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(
            CodeVerifierByteLength);

        return Base64Url.EncodeToString(
            bytes);
    }

    public static string ComputeCodeChallenge(string codeVerifier)
    {
        var bytes = SHA256.HashData(
            System.Text.Encoding.ASCII.GetBytes(codeVerifier));

        return Base64Url.EncodeToString(bytes);
    }

    public static async Task<IdTokenResult?> ValidateAndExtractIdToken(
        string? idToken,
        string expectedIssuer,
        string expectedAudience,
        string expectedNonce,
        IList<SecurityKey> signingKeys)
    {
        if (string.IsNullOrEmpty(idToken))
            return null;

        try
        {
            var handler = new JsonWebTokenHandler();

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = expectedIssuer,
                ValidateAudience = true,
                ValidAudience = expectedAudience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                RequireSignedTokens = true,
                IssuerSigningKeys = signingKeys
            };

            var result = await handler.ValidateTokenAsync(idToken, validationParameters);

            if (!result.IsValid)
            {
                Log.Warning(
                    result.Exception,
                    "id_token validation failed");

                return null;
            }

            var nonce = result.ClaimsIdentity.FindFirst("nonce")?.Value;

            if (nonce != expectedNonce)
            {
                Log.Warning("id_token nonce mismatch");
                return null;
            }

            var email = result.ClaimsIdentity.FindFirst(ClaimTypes.Email)?.Value
                ?? result.ClaimsIdentity.FindFirst("email")?.Value;

            var sub = result.ClaimsIdentity.FindFirst("sub")?.Value;

            return new IdTokenResult(Email: email, Sub: sub);
        }
        catch (Exception e)
        {
            Log.Warning(e, "Failed to validate id_token");
            return null;
        }
    }

    public record IdTokenResult(string? Email, string? Sub);
}
