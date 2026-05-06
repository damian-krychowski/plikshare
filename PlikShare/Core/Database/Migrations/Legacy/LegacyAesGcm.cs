using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using PlikShare.Core.Encryption;

namespace PlikShare.Core.Database.Migrations.Legacy;

/// <summary>
/// Slow-path AES-GCM cipher used by data re-encryption migrations only. Layout:
///   [MasterKeyId(1) | IterationsFactor(2 LE) | Salt(16) | Nonce(12) | Tag(16) | Ciphertext(N)]
/// Iterations = IterationsFactor * 10 000. PBKDF2-SHA256 derives a 32-byte AES key from the
/// master password (looked up by MasterKeyId).
///
/// This format was the production cipher up to Migration_34. The runtime no longer carries it —
/// it lives here so Migration_15 (CCM → slow GCM) and Migration_34 / Migration_Ai_02 (slow GCM →
/// fast GCM) can keep replaying on installations that still hold pre-migration rows.
/// </summary>
internal static class LegacyAesGcm
{
    private const int MasterKeyIdSize = 1;
    private const int IterationsFactorSize = 2;
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;
    private const int IterationsFactorWeight = 10000;
    private const int IterationsCountForNewEncryption = 650_000;
    private const int HeaderSize =
        MasterKeyIdSize + IterationsFactorSize + SaltSize + NonceSize + TagSize;

    public static byte[] Encrypt(string plainText, MasterEncryptionKeyProvider provider)
    {
        var masterKey = provider.GetCurrentEncryptionKey();
        var plaintextBytes = Encoding.UTF8.GetBytes(plainText);

        var salt = new byte[SaltSize];
        RandomNumberGenerator.Fill(salt);

        var derivedKey = new byte[KeySize];

        try
        {
            masterKey.PasswordBytes.Use(
                state: new DeriveState
                {
                    Salt = salt,
                    Output = derivedKey,
                    Iterations = IterationsCountForNewEncryption
                },
                action: static (pwSpan, s) =>
                {
                    Rfc2898DeriveBytes.Pbkdf2(
                        password: pwSpan,
                        salt: s.Salt,
                        destination: s.Output,
                        iterations: s.Iterations,
                        hashAlgorithm: HashAlgorithmName.SHA256);
                });

            var output = new byte[HeaderSize + plaintextBytes.Length];
            var span = output.AsSpan();

            var pos = 0;
            span[pos] = masterKey.Id;
            pos += MasterKeyIdSize;

            BinaryPrimitives.WriteUInt16LittleEndian(
                span.Slice(pos, IterationsFactorSize),
                (ushort)(IterationsCountForNewEncryption / IterationsFactorWeight));
            pos += IterationsFactorSize;

            salt.CopyTo(span.Slice(pos, SaltSize));
            pos += SaltSize;

            var nonce = span.Slice(pos, NonceSize);
            RandomNumberGenerator.Fill(nonce);
            pos += NonceSize;

            var tag = span.Slice(pos, TagSize);
            pos += TagSize;

            var ciphertext = span.Slice(pos, plaintextBytes.Length);

            using var aes = new AesGcm(derivedKey, TagSize);
            aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

            return output;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derivedKey);
        }
    }

    /// <summary>
    /// Decrypts the slow-path payload and returns the plaintext bytes. Caller is responsible
    /// for zeroing the returned array after use (the migrations do this in their UDFs).
    /// </summary>
    public static byte[] Decrypt(byte[] versionedBytes, MasterEncryptionKeyProvider provider)
    {
        if (versionedBytes.Length < HeaderSize)
            throw new InvalidOperationException(
                $"Slow-path payload is shorter than the fixed header ({HeaderSize} bytes).");

        var span = versionedBytes.AsSpan();
        var pos = 0;

        var keyId = span[pos];
        pos += MasterKeyIdSize;

        var iterations = BinaryPrimitives.ReadUInt16LittleEndian(
            span.Slice(pos, IterationsFactorSize)) * IterationsFactorWeight;
        pos += IterationsFactorSize;

        var salt = span.Slice(pos, SaltSize);
        pos += SaltSize;

        var nonce = span.Slice(pos, NonceSize);
        pos += NonceSize;

        var tag = span.Slice(pos, TagSize);
        pos += TagSize;

        var ciphertext = span.Slice(pos);

        var masterKey = provider.GetEncryptionKeyById(keyId);

        Span<byte> derivedKey = stackalloc byte[KeySize];

        try
        {
            masterKey.PasswordBytes.Use(
                state: new DeriveState
                {
                    Salt = salt,
                    Output = derivedKey,
                    Iterations = iterations
                },
                action: static (pwSpan, s) =>
                {
                    Rfc2898DeriveBytes.Pbkdf2(
                        password: pwSpan,
                        salt: s.Salt,
                        destination: s.Output,
                        iterations: s.Iterations,
                        hashAlgorithm: HashAlgorithmName.SHA256);
                });

            var plaintext = new byte[ciphertext.Length];

            using var aes = new AesGcm(derivedKey, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);

            return plaintext;
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
        public int Iterations { get; init; }
    }
}
