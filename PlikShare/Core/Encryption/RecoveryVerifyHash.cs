using System.Security.Cryptography;

namespace PlikShare.Core.Encryption;

/// <summary>
/// Domain-separated verification hash for a recovery seed.
/// Lets the server confirm a pasted recovery code matches the one bound to a storage,
/// without ever storing the seed itself.
/// </summary>
public static class RecoveryVerifyHash
{
    public const int HashSize = 32;
    private const int AuthKeySize = 32;

    private static readonly byte[] Info = "plikshare-recovery-verify\0"u8.ToArray();

    public static byte[] Compute(ReadOnlySpan<byte> recoveryBytes)
    {
        Span<byte> authKey = stackalloc byte[AuthKeySize];

        try
        {
            HKDF.DeriveKey(
                hashAlgorithmName: HashAlgorithmName.SHA256,
                ikm: recoveryBytes,
                output: authKey,
                salt: [],
                info: Info);

            return SHA256.HashData(authKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(authKey);
        }
    }

    public static bool Verify(ReadOnlySpan<byte> recoveryBytes, ReadOnlySpan<byte> expected)
    {
        if (expected.Length != HashSize)
            return false;

        var computed = Compute(recoveryBytes);

        try
        {
            return CryptographicOperations.FixedTimeEquals(computed, expected);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(computed);
        }
    }
}
