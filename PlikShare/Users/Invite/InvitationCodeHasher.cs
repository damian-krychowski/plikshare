using System.Security.Cryptography;
using PlikShare.Core.Utils;

namespace PlikShare.Users.Invite;

public static class InvitationCodeHasher
{
    public static byte[] Hash(string invitationCode)
    {
        ArgumentNullException.ThrowIfNull(invitationCode);

        var invitationCodeBytes = Base62Encoding.FromBase62ToBytes(invitationCode);

        return SHA256.HashData(invitationCodeBytes);
    }

    public static bool TryHash(string? invitationCode, out byte[] hash)
    {
        hash = [];

        if (string.IsNullOrEmpty(invitationCode))
            return false;

        if (!Base62Encoding.TryFromBase62ToBytes(invitationCode, out var bytes))
            return false;

        hash = SHA256.HashData(bytes);
        return true;
    }

    public static bool FixedTimeEquals(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
        => CryptographicOperations.FixedTimeEquals(a, b);
}
