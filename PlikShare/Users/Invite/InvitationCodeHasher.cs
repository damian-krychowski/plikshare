using System.Security.Cryptography;
using System.Text;

namespace PlikShare.Users.Invite;

public static class InvitationCodeHasher
{
    public static byte[] Hash(string invitationCode)
    {
        ArgumentNullException.ThrowIfNull(invitationCode);
        return SHA256.HashData(Encoding.UTF8.GetBytes(invitationCode));
    }

    public static bool FixedTimeEquals(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
        => CryptographicOperations.FixedTimeEquals(a, b);
}
