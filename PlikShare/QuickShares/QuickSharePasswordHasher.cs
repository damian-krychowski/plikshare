using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace PlikShare.QuickShares;

public class QuickSharePasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int TimeCost = 3;
    private const int MemoryCostKb = 64 * 1024;
    private const int Parallelism = 1;

    public async Task<(string Hash, byte[] Salt)> Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = await Derive(password, salt);
        return (Convert.ToBase64String(hash), salt);
    }

    public async Task<bool> Verify(string password, string expectedHashBase64, byte[] salt)
    {
        var actual = await Derive(password, salt);
        var expected = Convert.FromBase64String(expectedHashBase64);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static async Task<byte[]> Derive(string password, byte[] salt)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            using var argon2 = new Argon2id(passwordBytes)
            {
                Salt = salt,
                DegreeOfParallelism = Parallelism,
                Iterations = TimeCost,
                MemorySize = MemoryCostKb
            };

            return await argon2.GetBytesAsync(HashSize);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }
}
