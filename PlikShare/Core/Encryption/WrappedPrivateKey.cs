using System.Security.Cryptography;

namespace PlikShare.Core.Encryption;

/// <summary>
/// Wraps a user's X25519 private key with a symmetric KEK using AES-256-GCM.
/// Output layout: [nonce(12) | ciphertext(32) | tag(16)] = 60 bytes for a 32-byte private key.
///
/// The KEK can come from either:
/// - <see cref="EncryptionPasswordKdf"/> (Argon2id on encryption password) — everyday unwrap
/// - <see cref="UserEncryptionRecovery"/> (HKDF on recovery seed) — disaster recovery
///
/// Both paths wrap the same private key independently; either can unwrap it on its own.
/// </summary>
public static class WrappedPrivateKey
{
    public const int NonceSize = 12;
    public const int TagSize = 16;
    public const int KekSize = 32;

    public static byte[] Wrap(ReadOnlySpan<byte> kek, ReadOnlySpan<byte> privateKey)
    {
        if (kek.Length != KekSize)
            throw new ArgumentException(
                $"KEK must be {KekSize} bytes, got {kek.Length}.",
                nameof(kek));

        if (privateKey.Length != UserKeyPair.PrivateKeySize)
            throw new ArgumentException(
                $"Private key must be {UserKeyPair.PrivateKeySize} bytes, got {privateKey.Length}.",
                nameof(privateKey));

        var output = new byte[NonceSize + privateKey.Length + TagSize];
        Span<byte> nonce = output.AsSpan(0, NonceSize);
        Span<byte> ciphertext = output.AsSpan(NonceSize, privateKey.Length);
        Span<byte> tag = output.AsSpan(NonceSize + privateKey.Length, TagSize);

        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(kek, TagSize);
        aes.Encrypt(nonce, privateKey, ciphertext, tag);

        return output;
    }

    /// <summary>
    /// Unwraps a previously wrapped private key. Returns a <see cref="SecureBytes"/>
    /// (pinned, mlocked, zeroed on dispose) that the caller MUST dispose.
    /// Throws on tag-verification failure (wrong KEK or tampered ciphertext).
    /// </summary>
    public static SecureBytes Unwrap(ReadOnlySpan<byte> kek, ReadOnlySpan<byte> wrapped)
    {
        if (kek.Length != KekSize)
            throw new ArgumentException(
                $"KEK must be {KekSize} bytes, got {kek.Length}.",
                nameof(kek));

        var expectedSize = NonceSize + UserKeyPair.PrivateKeySize + TagSize;

        if (wrapped.Length != expectedSize)
            throw new ArgumentException(
                $"Wrapped private key must be {expectedSize} bytes, got {wrapped.Length}.",
                nameof(wrapped));

        return SecureBytes.Create(
            length: UserKeyPair.PrivateKeySize,
            state: new DecryptInput
            {
                Kek = kek,
                Nonce = wrapped[..NonceSize],
                Ciphertext = wrapped.Slice(NonceSize, UserKeyPair.PrivateKeySize),
                Tag = wrapped.Slice(NonceSize + UserKeyPair.PrivateKeySize, TagSize)
            },
            initializer: static (output, state) =>
            {
                using var aes = new AesGcm(
                    state.Kek, 
                    TagSize);

                aes.Decrypt(
                    state.Nonce, 
                    state.Ciphertext, 
                    state.Tag, 
                    output);
            });
    }

    private readonly ref struct DecryptInput
    {
        public required ReadOnlySpan<byte> Kek { get; init; }
        public required ReadOnlySpan<byte> Nonce { get; init; }
        public required ReadOnlySpan<byte> Ciphertext { get; init; }
        public required ReadOnlySpan<byte> Tag { get; init; }
    }
}