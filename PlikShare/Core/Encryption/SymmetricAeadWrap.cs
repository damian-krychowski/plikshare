using System.Security.Cryptography;

namespace PlikShare.Core.Encryption;

/// <summary>
/// Reusable AES-256-GCM wrap primitive. A caller-supplied 32-byte KEK protects an
/// arbitrary-length plaintext, typically another key (DEK, private key, seed).
///
/// Output layout: [nonce(12) | ciphertext | tag(16)]. Nonce is fresh-random per
/// wrap; with a 32-byte KEK and random 96-bit nonces, the collision bound is
/// comfortable for all the per-subject wraps we produce (one wrap per user ×
/// workspace × DEK version is a tiny number compared to 2^48).
///
/// Unwrap returns <see cref="SecureBytes"/> (pinned, mlocked, zeroed on dispose)
/// that the caller MUST dispose. Tag verification is authoritative: a wrong KEK
/// or tampered ciphertext throws out of <see cref="AesGcm.Decrypt"/>.
/// </summary>
public static class SymmetricAeadWrap
{
    public const int NonceSize = 12;
    public const int TagSize = 16;
    public const int KekSize = 32;

    public static byte[] Wrap(ReadOnlySpan<byte> kek, ReadOnlySpan<byte> plaintext)
    {
        if (kek.Length != KekSize)
            throw new ArgumentException(
                $"KEK must be {KekSize} bytes, got {kek.Length}.",
                nameof(kek));

        if (plaintext.IsEmpty)
            throw new ArgumentException("Plaintext must not be empty.", nameof(plaintext));

        var output = new byte[NonceSize + plaintext.Length + TagSize];
        Span<byte> nonce = output.AsSpan(0, NonceSize);
        Span<byte> ciphertext = output.AsSpan(NonceSize, plaintext.Length);
        Span<byte> tag = output.AsSpan(NonceSize + plaintext.Length, TagSize);

        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(kek, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        return output;
    }

    public static SecureBytes Unwrap(ReadOnlySpan<byte> kek, ReadOnlySpan<byte> wrapped)
    {
        if (kek.Length != KekSize)
            throw new ArgumentException(
                $"KEK must be {KekSize} bytes, got {kek.Length}.",
                nameof(kek));

        var minSize = NonceSize + TagSize;
        if (wrapped.Length <= minSize)
            throw new ArgumentException(
                $"Wrapped payload must be longer than {minSize} bytes, got {wrapped.Length}.",
                nameof(wrapped));

        var plaintextLength = wrapped.Length - NonceSize - TagSize;

        return SecureBytes.Create(
            length: plaintextLength,
            state: new DecryptInput
            {
                Kek = kek,
                Nonce = wrapped[..NonceSize],
                Ciphertext = wrapped.Slice(NonceSize, plaintextLength),
                Tag = wrapped.Slice(NonceSize + plaintextLength, TagSize)
            },
            initializer: static (output, state) =>
            {
                using var aes = new AesGcm(state.Kek, TagSize);
                aes.Decrypt(state.Nonce, state.Ciphertext, state.Tag, output);
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
