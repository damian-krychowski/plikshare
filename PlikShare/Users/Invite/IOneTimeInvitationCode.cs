using System.Security.Cryptography;
using PlikShare.Core.Utils;

namespace PlikShare.Users.Invite;

public interface IOneTimeInvitationCode
{
    string Generate();
}

public class OneTimeInvitationCode : IOneTimeInvitationCode
{
    // 256 bits of entropy — SHA-256 collision and preimage attacks are both
    // computationally infeasible at this size, which is what lets us store only
    // the hash without a KDF.
    public const int EntropyBytes = 32;

    public string Generate() => RandomNumberGenerator.GetBytes(EntropyBytes).ToBase62();
}
