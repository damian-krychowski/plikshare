using NSec.Cryptography;

namespace PlikShare.Core.Encryption;

/// <summary>
/// Per-user asymmetric keypair for wrapping scoped DEKs (workspace, storage, box, link) to a recipient
/// who can later unwrap them with their private key. Built on X25519 key agreement composed with
/// HKDF-SHA256 + ChaCha20-Poly1305 as a sealed-box AEAD.
///
/// The public key is stored in clear in the DB. The private key is never stored in clear —
/// it is wrapped with a KEK derived from the user's encryption password (see EncryptionPasswordKdf),
/// and additionally with a recovery KEK derived from a BIP-39 recovery code.
///
/// Sealed-box semantics: the sender needs only the recipient's public key to wrap a payload.
/// The recipient needs their private key to unwrap. There is no pre-shared secret and no handshake.
/// This is the same primitive libsodium exposes as <c>crypto_box_seal</c>.
/// </summary>
public static class UserKeyPair
{
    public const int PublicKeySize = 32;
    public const int PrivateKeySize = 32;

    private static readonly KeyAgreementAlgorithm KeyAgreement = KeyAgreementAlgorithm.X25519;
    private static readonly KeyDerivationAlgorithm Kdf = KeyDerivationAlgorithm.HkdfSha256;
    private static readonly AeadAlgorithm Aead = AeadAlgorithm.ChaCha20Poly1305;

    private const int AeadKeySize = 32;
    private const int AeadNonceSize = 12;

    private static readonly byte[] SealedBoxInfo = "plikshare-sealed-box-v1\0"u8.ToArray();

    public readonly record struct KeyMaterial(byte[] PublicKey, byte[] PrivateKey);

    /// <summary>
    /// Generates a fresh X25519 keypair. Returns raw key bytes (32 + 32) — caller is responsible
    /// for persisting the public key and wrapping the private key with the user's encryption KEK.
    /// </summary>
    public static KeyMaterial Generate()
    {
        using var key = Key.Create(
            KeyAgreement,
            new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });

        var publicKey = key.PublicKey.Export(KeyBlobFormat.RawPublicKey);
        var privateKey = key.Export(KeyBlobFormat.RawPrivateKey);

        return new KeyMaterial(publicKey, privateKey);
    }

    /// <summary>
    /// Wraps a payload for a recipient whose public key is known. The sender does not need to
    /// know the recipient's private key or password — only their public key.
    ///
    /// Output layout: [ephemeralPublicKey(32) | nonce(12) | ciphertext | tag(16)].
    /// The recipient reconstructs the shared secret from their private key + the ephemeral
    /// public key embedded at the start, derives the symmetric key via HKDF, and decrypts.
    /// </summary>
    public static byte[] SealTo(ReadOnlySpan<byte> recipientPublicKey, ReadOnlySpan<byte> plaintext)
    {
        if (recipientPublicKey.Length != PublicKeySize)
            throw new ArgumentException(
                $"Recipient public key must be {PublicKeySize} bytes, got {recipientPublicKey.Length}.",
                nameof(recipientPublicKey));

        var recipient = PublicKey.Import(KeyAgreement, recipientPublicKey, KeyBlobFormat.RawPublicKey);

        using var ephemeral = Key.Create(
            KeyAgreement,
            new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });

        var ephemeralPublicKey = ephemeral.PublicKey.Export(KeyBlobFormat.RawPublicKey);

        using var sharedSecret = KeyAgreement.Agree(ephemeral, recipient)
            ?? throw new InvalidOperationException(
                "X25519 agreement failed — recipient public key is likely a low-order point.");

        // HKDF info binds the derivation to the two public keys so the same shared secret
        // cannot be reused under a different context. Salt left empty per sealed-box convention.
        Span<byte> hkdfInfo = stackalloc byte[SealedBoxInfo.Length + PublicKeySize * 2];
        SealedBoxInfo.CopyTo(hkdfInfo);
        ephemeralPublicKey.CopyTo(hkdfInfo[SealedBoxInfo.Length..]);
        recipientPublicKey.CopyTo(hkdfInfo[(SealedBoxInfo.Length + PublicKeySize)..]);

        using var symmetricKey = Kdf.DeriveKey(
            sharedSecret,
            salt: [],
            info: hkdfInfo,
            algorithm: Aead,
            creationParameters: new KeyCreationParameters { ExportPolicy = KeyExportPolicies.None });

        var nonce = new byte[AeadNonceSize];
        System.Security.Cryptography.RandomNumberGenerator.Fill(nonce);

        var ciphertextWithTag = Aead.Encrypt(symmetricKey, nonce, associatedData: [], plaintext);

        var output = new byte[PublicKeySize + AeadNonceSize + ciphertextWithTag.Length];
        ephemeralPublicKey.CopyTo(output, 0);
        nonce.CopyTo(output, PublicKeySize);
        ciphertextWithTag.CopyTo(output, PublicKeySize + AeadNonceSize);

        return output;
    }

    /// <summary>
    /// Unwraps a payload that was sealed to this user's public key, using this user's private key.
    /// Parses the layout produced by <see cref="SealTo"/>, reconstructs the shared secret,
    /// and decrypts with the symmetric AEAD. Throws on tag-verification failure.
    /// </summary>
    public static byte[] OpenSealed(ReadOnlySpan<byte> recipientPrivateKey, ReadOnlySpan<byte> sealed_)
    {
        if (recipientPrivateKey.Length != PrivateKeySize)
            throw new ArgumentException(
                $"Recipient private key must be {PrivateKeySize} bytes, got {recipientPrivateKey.Length}.",
                nameof(recipientPrivateKey));

        var minLength = PublicKeySize + AeadNonceSize + Aead.TagSize;
        if (sealed_.Length < minLength)
            throw new ArgumentException(
                $"Sealed payload too short: {sealed_.Length} bytes (minimum {minLength}).",
                nameof(sealed_));

        var ephemeralPublicKeyBytes = sealed_[..PublicKeySize];
        var nonce = sealed_.Slice(PublicKeySize, AeadNonceSize);
        var ciphertextWithTag = sealed_[(PublicKeySize + AeadNonceSize)..];

        using var recipient = Key.Import(KeyAgreement, recipientPrivateKey, KeyBlobFormat.RawPrivateKey);
        var ephemeralPublicKey = PublicKey.Import(KeyAgreement, ephemeralPublicKeyBytes, KeyBlobFormat.RawPublicKey);
        var recipientPublicKey = recipient.PublicKey.Export(KeyBlobFormat.RawPublicKey);

        using var sharedSecret = KeyAgreement.Agree(recipient, ephemeralPublicKey)
            ?? throw new InvalidOperationException(
                "X25519 agreement failed — sealed payload is likely malformed or tampered.");

        Span<byte> hkdfInfo = stackalloc byte[SealedBoxInfo.Length + PublicKeySize * 2];
        SealedBoxInfo.CopyTo(hkdfInfo);
        ephemeralPublicKeyBytes.CopyTo(hkdfInfo[SealedBoxInfo.Length..]);
        recipientPublicKey.CopyTo(hkdfInfo[(SealedBoxInfo.Length + PublicKeySize)..]);

        using var symmetricKey = Kdf.DeriveKey(
            sharedSecret,
            salt: [],
            info: hkdfInfo,
            algorithm: Aead,
            creationParameters: new KeyCreationParameters { ExportPolicy = KeyExportPolicies.None });

        return Aead.Decrypt(symmetricKey, nonce, associatedData: [], ciphertextWithTag)
            ?? throw new InvalidOperationException(
                "Sealed payload failed AEAD verification — wrong private key, or payload tampered.");
    }
}
