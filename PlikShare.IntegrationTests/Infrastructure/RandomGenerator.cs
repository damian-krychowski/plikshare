using PlikShare.Core.Utils;
using PlikShare.Users.Invite;

namespace PlikShare.IntegrationTests.Infrastructure;

public class RandomGenerator
{
    private readonly OneTimeInvitationCode _realOneTimeInvitationCode = new();

    public string Email(string? prefix = null)
    {
        var pref = prefix is null ? "" : $"{prefix}_";

        return $"{pref}email_{Guid.NewGuid().ToBase62().ToLowerInvariant()}@plikshare.com";
    }

    public string InvitationCode(string? prefix = null)
    {
        var pref = prefix is null ? "" : $"{prefix}_";

        return $"{pref}invitation_code_{Guid.NewGuid().ToBase62()}";
    }

    /// <summary>
    /// Produces an invitation code in the exact Base62-of-32-random-bytes shape the
    /// production <see cref="OneTimeInvitationCode"/> generator emits. Required for
    /// tests that need to exercise the full-encryption ephemeral path — the ephemeral
    /// DEK KEK is derived by decoding the code from Base62, which rejects prefixes or
    /// underscores that <see cref="InvitationCode"/> injects for readability.
    /// </summary>
    public string RealShapeInvitationCode() => _realOneTimeInvitationCode.Generate();

    public string Password()
    {
        return $"password_{Guid.NewGuid().ToBase62()}_%Aa123";
    }

    public string Name(string baseName)
    {
        return $"{baseName}-{Guid.NewGuid().ToBase62()}";
    }

    public string AuthCode()
    {
        return $"code_{Guid.NewGuid().ToBase62()}";
    }

    public string Sub()
    {
        return $"sub_{Guid.NewGuid().ToBase62()}";
    }

    public string ClientId()
    {
        return $"client_{Guid.NewGuid().ToBase62()}";
    }

    public string ClientSecret()
    {
        return $"secret_{Guid.NewGuid().ToBase62()}";
    }

    public byte[] Bytes(int length)
    {
        var buffer = new byte[length];
        System.Security.Cryptography.RandomNumberGenerator.Fill(buffer);
        return buffer;
    }
}