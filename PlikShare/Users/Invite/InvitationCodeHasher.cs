using System.Security.Cryptography;
using PlikShare.Core.Utils;

namespace PlikShare.Users.Invite;

public static class InvitationCodeHasher
{
    public static byte[] Hash(string invitationCode)
    {
        ArgumentNullException.ThrowIfNull(invitationCode);

        var invitationCodeBytes = Base62Encoding.FromBase62ToBytes(
            invitationCode);

        return SHA256.HashData(invitationCodeBytes);
    }

    public static bool FixedTimeEquals(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
        => CryptographicOperations.FixedTimeEquals(a, b);
}
