using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using PlikShare.Core.Utils;

namespace PlikShare.Core.Encryption;

/// <summary>
/// Derives a user's encryption KEK from their encryption password via Argon2id.
/// The KEK is used to wrap/unwrap the user's X25519 private key.
///
/// Parameters are versioned and stored alongside the wrapped key in the DB so we can
/// migrate to harder params in the future without breaking existing users — new users
/// get new params, old users keep theirs until they change password (which re-derives
/// with the current defaults).
/// </summary>
public static class EncryptionPasswordKdf
{
    public const int SaltSize = 32;
    public const int KekSize = 32;

    /// <summary>
    /// Argon2id parameters. Higher memory = harder to parallelize on GPU/ASIC.
    /// Defaults chosen for server-side derivation: ~300-500ms per unlock on a modern CPU,
    /// 64 MiB of memory per operation.
    /// </summary>
    public readonly record struct Params(
        int TimeCost,
        int MemoryCostKb,
        int Parallelism)
    {
        public static Params Default => new(
            TimeCost: 3,
            MemoryCostKb: 64 * 1024,
            Parallelism: 1);
    }

    public static byte[] GenerateSalt() => RandomNumberGenerator.GetBytes(SaltSize);

    /// <summary>
    /// Derives the KEK into a <see cref="SecureBytes"/> buffer (pinned, mlocked, zeroed
    /// on dispose) that the caller MUST dispose. Argon2id itself returns a byte[] on the
    /// managed heap which we zero immediately after copying — a brief (microsecond) window
    /// of plaintext KEK on the unpinned heap is inherent to the Konscious.Security.Cryptography
    /// API and not worth bypassing.
    /// </summary>
    public static async Task<SecureBytes> DeriveKek(string password, byte[] salt, Params parameters)
    {
        if (salt.Length != SaltSize)
            throw new ArgumentException(
                $"Salt must be {SaltSize} bytes, got {salt.Length}.",
                nameof(salt));

        var passwordBytes = Encoding.UTF8.GetBytes(
            password);

        try
        {
            using var argon2 = new Argon2id(passwordBytes)
            {
                Salt = salt,
                DegreeOfParallelism = parameters.Parallelism,
                Iterations = parameters.TimeCost,
                MemorySize = parameters.MemoryCostKb
            };

            var kekBytes = await argon2.GetBytesAsync(
                KekSize);

            try
            {
                return SecureBytes.CopyFrom(kekBytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(kekBytes);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    /// <summary>
    /// Verify hash over the derived KEK — lets us reject a wrong password cheaply without
    /// attempting to unwrap the private key and waiting for AEAD tag failure. Not a password
    /// hash: it's a function of the KEK, which itself is a function of the password + salt + params.
    /// </summary>
    public static byte[] ComputeVerifyHash(ReadOnlySpan<byte> kek)
    {
        Span<byte> authKey = stackalloc byte[32];

        HKDF.DeriveKey(
            hashAlgorithmName: HashAlgorithmName.SHA256,
            ikm: kek,
            output: authKey,
            salt: [],
            info: "plikshare-user-encryption-kek-verify\0"u8);

        return SHA256.HashData(authKey);
    }

    public static bool Verify(ReadOnlySpan<byte> kek, ReadOnlySpan<byte> expectedHash)
    {
        var actual = ComputeVerifyHash(kek);
        return CryptographicOperations.FixedTimeEquals(actual, expectedHash);
    }

    /// <summary>
    /// Serializes params as compact JSON for storage in u_encryption_kdf_params.
    /// </summary>
    public static string SerializeParams(Params parameters)
        => Json.Serialize(parameters);

    public static Params DeserializeParams(string serialized)
    {
        var result = Json.Deserialize<Params>(serialized);
        if (result.Equals(default(Params)))
            throw new InvalidOperationException(
                $"Failed to deserialize Argon2id params from '{serialized}'.");
        return result;
    }
}