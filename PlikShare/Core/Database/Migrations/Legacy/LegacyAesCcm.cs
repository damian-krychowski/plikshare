using System.Security.Cryptography;
using System.Text;
using PlikShare.Core.Encryption;

namespace PlikShare.Core.Database.Migrations.Legacy;

/// <summary>
/// Legacy AES-CCM cipher used by Migration_15 only. Layout (versioned bytes):
///   [KeyId(1) | Salt(16) | Nonce(12) | Tag(16) | Ciphertext(N)]
/// PBKDF2-SHA256 with 10 000 iterations derives a 32-byte key from the configured master
/// password (looked up by KeyId).
///
/// This format predates the slow-path AES-GCM cipher. Migration_15 is the only consumer; it
/// re-encrypts CCM rows into the slow-path GCM format so a subsequent migration (34) can carry
/// them forward to the fast-path. The runtime no longer carries this code.
/// </summary>
internal static class LegacyAesCcm
{
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 10000;

    public static string Decrypt(byte[] versionedBytes, MasterEncryptionKeyProvider provider)
    {
        var keyId = versionedBytes[0];
        var masterKey = provider.GetEncryptionKeyById(keyId);

        var inner = versionedBytes.AsSpan(1);
        var salt = inner.Slice(0, SaltSize);
        var nonce = inner.Slice(SaltSize, NonceSize);
        var tag = inner.Slice(SaltSize + NonceSize, TagSize);
        var ciphertext = inner.Slice(SaltSize + NonceSize + TagSize);

        var derivedKey = new byte[KeySize];

        try
        {
            masterKey.PasswordBytes.Use(
                state: new DeriveState
                {
                    Salt = salt,
                    Output = derivedKey
                },
                action: static (pwSpan, s) =>
                {
                    Rfc2898DeriveBytes.Pbkdf2(
                        password: pwSpan,
                        salt: s.Salt,
                        destination: s.Output,
                        iterations: Iterations,
                        hashAlgorithm: HashAlgorithmName.SHA256);
                });

            using var aes = new AesCcm(derivedKey);
            var plaintext = new byte[ciphertext.Length];
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
            return Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derivedKey);
        }
    }

    private readonly ref struct DeriveState
    {
        public ReadOnlySpan<byte> Salt { get; init; }
        public Span<byte> Output { get; init; }
    }
}
