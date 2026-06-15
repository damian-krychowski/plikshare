using System.Security.Cryptography;
using System.Text;
using PlikShare.Core.Utils;

namespace PlikShare.Agents.Authorization;

public class AgentTokenService
{
    public const string Prefix = "psh_agt_";

    private const int SecretByteLength = 32;

    public AgentTokenParts Generate()
    {
        var secret = RandomNumberGenerator
            .GetBytes(SecretByteLength)
            .ToBase62();

        var token = $"{Prefix}{secret}";

        return new AgentTokenParts(
            Token: token,
            Hash: Hash(token),
            Masked: Mask(token));
    }

    public string Hash(string token)
    {
        var hashBytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(token));

        return Convert.ToHexString(hashBytes);
    }

    private static string Mask(string token)
    {
        var suffix = token.Length >= 4
            ? token[^4..]
            : token;

        return $"{Prefix}…{suffix}";
    }
}

public readonly record struct AgentTokenParts(
    string Token,
    string Hash,
    string Masked);
