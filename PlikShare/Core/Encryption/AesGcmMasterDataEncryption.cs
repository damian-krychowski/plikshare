using System.Security.Cryptography;

namespace PlikShare.Core.Encryption;

public class AesGcmMasterDataEncryption(MasterEncryptionKeyProvider masterEncryptionKeyProvider) : IMasterDataEncryption
{
    private const int FormatVersionSize = 1;
    private const int MasterKeyIdSize = 1;
    private const int TagSize = 16;
    private const int NonceSize = 12;

    /// <summary>
    /// Fast-path format version marker. Version 1 layout:
    ///   [FormatVersion(1)=0x01 | MasterKeyId(1) | Nonce(12) | Tag(16) | Ciphertext(N)]
    /// The AES key is the process-wide stretched master key from
    /// <see cref="MasterEncryptionKeyProvider.GetStretchedKey"/>, derived once at startup
    /// via PBKDF2 over a fixed domain separator. Each encryption only needs a unique nonce,
    /// not a unique key.
    ///
    /// Future versions may change the layout — the first byte tells the decoder which
    /// parser and crypto primitives to use. Decoders MUST validate the marker before
    /// trusting anything else in the frame.
    /// </summary>
    private const byte FastFormatVersionV1 = 0x01;

    private const int FastHeaderSize =
        FormatVersionSize + MasterKeyIdSize + NonceSize + TagSize;

    public int GetEncryptedLength(int plaintextLength) =>
        FastHeaderSize + plaintextLength;

    public byte[] EncryptBytes(ReadOnlySpan<byte> plaintext)
    {
        var output = new byte[GetEncryptedLength(plaintext.Length)];
        EncryptBytes(plaintext, output);
        return output;
    }

    public void EncryptBytes(ReadOnlySpan<byte> plaintext, Span<byte> destination)
    {
        var expectedLength = GetEncryptedLength(plaintext.Length);

        if (destination.Length != expectedLength)
            throw new ArgumentException(
                $"Destination span length {destination.Length} does not match " +
                $"the expected ciphertext frame length {expectedLength}. " +
                $"Use {nameof(GetEncryptedLength)} to size the buffer correctly.",
                nameof(destination));

        var masterKey = masterEncryptionKeyProvider.GetCurrentEncryptionKey();
        var stretchedKey = masterEncryptionKeyProvider.GetStretchedKey(masterKey.Id);

        destination[0] = FastFormatVersionV1;
        destination[FormatVersionSize] = masterKey.Id;

        var nonceSpan = destination.Slice(FormatVersionSize + MasterKeyIdSize, NonceSize);
        var tagSpan = destination.Slice(FormatVersionSize + MasterKeyIdSize + NonceSize, TagSize);
        var ciphertextSpan = destination.Slice(FastHeaderSize, plaintext.Length);

        RandomNumberGenerator.Fill(nonceSpan);

        stretchedKey.Use(
            state: new FastEncryptState
            {
                Nonce = nonceSpan,
                Tag = tagSpan,
                Plaintext = plaintext,
                Ciphertext = ciphertextSpan
            },
            action: static (keySpan, s) =>
            {
                using var aes = new AesGcm(keySpan, TagSize);

                aes.Encrypt(
                    nonce: s.Nonce,
                    plaintext: s.Plaintext,
                    ciphertext: s.Ciphertext,
                    tag: s.Tag);
            });
    }

    public void DecryptBytes(ReadOnlySpan<byte> versionedEncryptedBytes, Span<byte> destination)
    {
        var frame = FastCiphertextFrame.FromBytes(versionedEncryptedBytes);

        if (destination.Length != frame.Ciphertext.Length)
            throw new ArgumentException(
                $"Destination span length {destination.Length} does not match " +
                $"the ciphertext length {frame.Ciphertext.Length}. " +
                $"Use {nameof(GetDecryptedLength)} to size the buffer correctly.",
                nameof(destination));

        var stretchedKey = masterEncryptionKeyProvider.GetStretchedKey(frame.MasterKeyId);

        stretchedKey.Use(
            state: new FastDecryptState
            {
                Nonce = frame.Nonce,
                Tag = frame.Tag,
                Ciphertext = frame.Ciphertext,
                Plaintext = destination
            },
            action: static (keySpan, s) =>
            {
                using var aes = new AesGcm(keySpan, TagSize);
                aes.Decrypt(
                    nonce: s.Nonce,
                    ciphertext: s.Ciphertext,
                    tag: s.Tag,
                    plaintext: s.Plaintext);
            });
    }

    public int GetDecryptedLength(ReadOnlySpan<byte> versionedEncryptedBytes)
    {
        if (versionedEncryptedBytes.Length < FastHeaderSize)
            throw new ArgumentException(
                $"Fast-path encrypted payload is shorter than the fixed header ({FastHeaderSize} bytes).",
                nameof(versionedEncryptedBytes));

        return versionedEncryptedBytes.Length - FastHeaderSize;
    }

    private readonly ref struct FastCiphertextFrame
    {
        public byte MasterKeyId { get; private init; }
        public ReadOnlySpan<byte> Nonce { get; private init; }
        public ReadOnlySpan<byte> Tag { get; private init; }
        public ReadOnlySpan<byte> Ciphertext { get; private init; }

        public static FastCiphertextFrame FromBytes(ReadOnlySpan<byte> versionedEncryptedBytes)
        {
            if (versionedEncryptedBytes.Length < FastHeaderSize)
                throw new ArgumentException(
                    $"Fast-path encrypted payload is shorter than the fixed header ({FastHeaderSize} bytes).",
                    nameof(versionedEncryptedBytes));

            var formatVersion = versionedEncryptedBytes[0];
            if (formatVersion != FastFormatVersionV1)
                throw new InvalidOperationException(
                    $"Unsupported fast-path format version 0x{formatVersion:X2}. " +
                    $"Expected 0x{FastFormatVersionV1:X2}.");

            return new FastCiphertextFrame
            {
                MasterKeyId = versionedEncryptedBytes[FormatVersionSize],
                Nonce = versionedEncryptedBytes.Slice(
                    FormatVersionSize + MasterKeyIdSize, NonceSize),
                Tag = versionedEncryptedBytes.Slice(
                    FormatVersionSize + MasterKeyIdSize + NonceSize, TagSize),
                Ciphertext = versionedEncryptedBytes[FastHeaderSize..]
            };
        }
    }

    private readonly ref struct FastEncryptState
    {
        public Span<byte> Nonce { get; init; }
        public Span<byte> Tag { get; init; }
        public ReadOnlySpan<byte> Plaintext { get; init; }
        public Span<byte> Ciphertext { get; init; }
    }

    private readonly ref struct FastDecryptState
    {
        public ReadOnlySpan<byte> Nonce { get; init; }
        public ReadOnlySpan<byte> Tag { get; init; }
        public ReadOnlySpan<byte> Ciphertext { get; init; }
        public Span<byte> Plaintext { get; init; }
    }
}
