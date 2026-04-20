using System.Buffers.Text;
using System.Security.Cryptography;

namespace PlikShare.Users.Invite;

public interface IOneTimeInvitationCode
{
    string Generate();
}

public class OneTimeInvitationCode : IOneTimeInvitationCode
{
    // 256 bits of entropy — HMAC-SHA256 hash collision and brute-force are both
    // computationally infeasible at this size, which is what lets us store only
    // the hash without a KDF.
    private const int EntropyBytes = 32;

    public string Generate() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(EntropyBytes));
}
