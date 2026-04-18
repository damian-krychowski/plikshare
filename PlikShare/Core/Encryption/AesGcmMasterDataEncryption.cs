using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace PlikShare.Core.Encryption;

public class AesGcmMasterDataEncryption(MasterEncryptionKeyProvider masterEncryptionKeyProvider) : IMasterDataEncryption
{
    private const int FormatVersionSize = 1;
    private const int MasterKeyIdSize = 1;
    private const int SaltSize = 16;
    private const int TagSize = 16;
    private const int NonceSize = 12;
    private const int IterationsFactorSize = 2;
    private const int EncryptionKeySize = 32; // 256-bit key

    private const int IterationsFactorWeight = 10000;
    private const int IterationsCountForNewEncryption = 650000;

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

    public byte[] Encrypt(string plainText)
    {
        var masterKey = masterEncryptionKeyProvider
            .GetCurrentEncryptionKey();

        Span<byte> salt = stackalloc byte[SaltSize];
        Span<byte> encryptionKey = stackalloc byte[EncryptionKeySize];

        try
        {
            RandomNumberGenerator.Fill(salt);

            masterKey.PasswordBytes.Use(
                state: new Pbkdf2State
                {
                    Salt = salt,
                    Output = encryptionKey,
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

            return Encrypt(
                plainText: plainText,
                masterKeyId: masterKey.Id,
                salt: salt,
                encryptionKey: encryptionKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encryptionKey);
        }
    }

    private static byte[] Encrypt(
        string plainText,
        byte masterKeyId,
        ReadOnlySpan<byte> salt,
        ReadOnlySpan<byte> encryptionKey)
    {
        var plaintextBytesCount = Encoding.UTF8.GetByteCount(plainText);
        var plaintextBuffer = ArrayPool<byte>.Shared.Rent(plaintextBytesCount);

        try
        {
            var plaintextSpan = plaintextBuffer.AsSpan(0, plaintextBytesCount);
            Encoding.UTF8.GetBytes(plainText, plaintextSpan);

            var versionedEncryptedBytesSize =
                MasterKeyIdSize
                + IterationsFactorSize
                + SaltSize
                + NonceSize
                + TagSize
                + plaintextBytesCount;

            var versionedEncryptedBytes = new byte[versionedEncryptedBytesSize];
            var versionedEncryptedBytesSpan = versionedEncryptedBytes.AsSpan();

            using var aes = new AesGcm(
                key: encryptionKey,
                tagSizeInBytes: TagSize);

            var position = 0;

            //1. MasterKeyId
            versionedEncryptedBytesSpan[position] = masterKeyId;
            position += MasterKeyIdSize;

            //2. IterationsFactor
            var iterationsFactorValue = (ushort)(IterationsCountForNewEncryption / IterationsFactorWeight);

            BinaryPrimitives.WriteUInt16LittleEndian(
                versionedEncryptedBytesSpan.Slice(position, IterationsFactorSize),
                iterationsFactorValue);

            position += IterationsFactorSize;

            //3. Salt
            salt.CopyTo(versionedEncryptedBytesSpan.Slice(position, SaltSize));
            position += SaltSize;

            //4. Nonce
            var nonceSpan = versionedEncryptedBytesSpan.Slice(position, NonceSize);
            RandomNumberGenerator.Fill(nonceSpan);
            position += NonceSize;

            //5. Tag
            var tagSpan = versionedEncryptedBytesSpan.Slice(position, TagSize);
            position += TagSize;

            //6. Ciphertext
            var ciphertextSpan = versionedEncryptedBytesSpan.Slice(position, plaintextBytesCount);

            aes.Encrypt(
                nonce: nonceSpan,
                plaintext: plaintextSpan,
                ciphertext: ciphertextSpan,
                tag: tagSpan);

            return versionedEncryptedBytes;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintextBuffer);
            ArrayPool<byte>.Shared.Return(plaintextBuffer);
        }
    }

    public string Decrypt(byte[] versionedEncryptedBytes)
    {
        var gcmCiphertext = AesGcmCiphertextStr.FromBytes(
            versionedEncryptedBytes: versionedEncryptedBytes);

        var masterKey = masterEncryptionKeyProvider.GetEncryptionKeyById(
            gcmCiphertext.MasterKeyId);

        Span<byte> encryptionKey = stackalloc byte[EncryptionKeySize];

        try
        {
            masterKey.PasswordBytes.Use(
                state: new Pbkdf2State
                {
                    Salt = gcmCiphertext.Salt,
                    Output = encryptionKey,
                    Iterations = gcmCiphertext.Iterations
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

            return Decrypt(encryptionKey, gcmCiphertext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encryptionKey);
        }
    }

    public byte[] FastEncryptBytes(ReadOnlySpan<byte> plaintext)
    {
        var masterKey = masterEncryptionKeyProvider.GetCurrentEncryptionKey();
        var stretchedKey = masterEncryptionKeyProvider.GetStretchedKey(masterKey.Id);

        var output = new byte[FastHeaderSize + plaintext.Length];
        var outputSpan = output.AsSpan();

        outputSpan[0] = FastFormatVersionV1;
        outputSpan[FormatVersionSize] = masterKey.Id;

        var nonceSpan = outputSpan.Slice(FormatVersionSize + MasterKeyIdSize, NonceSize);
        var tagSpan = outputSpan.Slice(FormatVersionSize + MasterKeyIdSize + NonceSize, TagSize);
        var ciphertextSpan = outputSpan.Slice(FastHeaderSize, plaintext.Length);

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

        return output;
    }

    public void FastDecryptBytes(byte[] versionedEncryptedBytes, Span<byte> destination)
    {
        var frame = FastCiphertextFrame.FromBytes(versionedEncryptedBytes);

        if (destination.Length != frame.Ciphertext.Length)
            throw new ArgumentException(
                $"Destination span length {destination.Length} does not match " +
                $"the ciphertext length {frame.Ciphertext.Length}. " +
                $"Use {nameof(GetFastDecryptedLength)} to size the buffer correctly.",
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

    public int GetFastDecryptedLength(byte[] versionedEncryptedBytes)
    {
        if (versionedEncryptedBytes.Length < FastHeaderSize)
            throw new ArgumentException(
                $"Fast-path encrypted payload is shorter than the fixed header ({FastHeaderSize} bytes).",
                nameof(versionedEncryptedBytes));

        return versionedEncryptedBytes.Length - FastHeaderSize;
    }

    private static string Decrypt(
        ReadOnlySpan<byte> encryptionKey,
        AesGcmCiphertextStr gcmCiphertext)
    {
        var heapBuffer = ArrayPool<byte>.Shared.Rent(
            minimumLength: gcmCiphertext.Ciphertext.Length);

        var plaintextBuffer = heapBuffer.AsSpan().Slice(
            start: 0,
            length: gcmCiphertext.Ciphertext.Length);

        try
        {
            using var aes = new AesGcm(
                key: encryptionKey,
                tagSizeInBytes: TagSize);

            aes.Decrypt(
                nonce: gcmCiphertext.Nonce,
                ciphertext: gcmCiphertext.Ciphertext,
                tag: gcmCiphertext.Tag,
                plaintext: plaintextBuffer);

            return Encoding.UTF8.GetString(
                plaintextBuffer);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(heapBuffer);
            ArrayPool<byte>.Shared.Return(heapBuffer);
        }
    }

    public IDerivedMasterDataEncryption NewDerived()
    {
        var masterKey = masterEncryptionKeyProvider
            .GetCurrentEncryptionKey();

        var salt = new byte[SaltSize];
        var encryptionKey = new byte[EncryptionKeySize];

        RandomNumberGenerator.Fill(salt);

        masterKey.PasswordBytes.Use(
            state: new Pbkdf2State
            {
                Salt = salt,
                Output = encryptionKey,
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

        return new AesGcmDerivedMasterDataEncryption(
            masterKeyId: masterKey.Id,
            salt: salt,
            encryptionKey: encryptionKey);
    }

    public IDerivedMasterDataEncryption DerivedFrom(byte[] versionedEncryptedBytes)
    {
        var gcmCiphertext = AesGcmCiphertextStr.FromBytes(
            versionedEncryptedBytes: versionedEncryptedBytes);

        var masterKey = masterEncryptionKeyProvider.GetEncryptionKeyById(
            gcmCiphertext.MasterKeyId);

        var encryptionKey = new byte[EncryptionKeySize];

        masterKey.PasswordBytes.Use(
            state: new Pbkdf2State
            {
                Salt = gcmCiphertext.Salt,
                Output = encryptionKey,
                Iterations = gcmCiphertext.Iterations
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

        return new AesGcmDerivedMasterDataEncryption(
            masterKeyId: masterKey.Id,
            salt: gcmCiphertext.Salt.ToArray(),
            encryptionKey: encryptionKey);
    }

    public IDerivedMasterDataEncryption DeserializeDerived(byte[] serialized)
    {
        return AesGcmDerivedMasterDataEncryption.Deserialize(serialized);
    }

    private class AesGcmDerivedMasterDataEncryption(
        byte masterKeyId,
        byte[] salt,
        byte[] encryptionKey) : IDerivedMasterDataEncryption
    {
        public byte[] Encrypt(string plainText)
        {
            return AesGcmMasterDataEncryption.Encrypt(
                plainText,
                masterKeyId,
                salt,
                encryptionKey);
        }

        public string Decrypt(byte[] versionedEncryptedBytes)
        {
            var gcmCiphertext = AesGcmCiphertextStr.FromBytes(
                versionedEncryptedBytes);

            if (!gcmCiphertext.Salt.SequenceEqual(salt) || gcmCiphertext.MasterKeyId != masterKeyId)
            {
                throw new InvalidOperationException(
                    "Cannot decrypt provided bytes with DerivedMasterDataEncryption because they do not belong to the correct family.");
            }

            return AesGcmMasterDataEncryption.Decrypt(
                encryptionKey,
                gcmCiphertext);
        }

        public byte[] Serialize()
        {
            var bytes = new byte[1 + salt.Length + encryptionKey.Length];

            var bytesSpan = bytes.AsSpan();

            bytesSpan[0] = masterKeyId;
            salt.CopyTo(bytesSpan.Slice(1, salt.Length));
            encryptionKey.CopyTo(bytesSpan.Slice(1 + salt.Length, encryptionKey.Length));

            return bytes;
        }

        public static AesGcmDerivedMasterDataEncryption Deserialize(byte[] serialized)
        {
            var span = serialized.AsSpan();

            return new AesGcmDerivedMasterDataEncryption(
                masterKeyId: span[0],
                salt: span.Slice(1, SaltSize).ToArray(),
                encryptionKey: span.Slice(1 + SaltSize).ToArray());
        }
    }

    private readonly ref struct AesGcmCiphertextStr
    {
        public byte MasterKeyId { get; private init; }
        public ReadOnlySpan<byte> IterationsFactor { get; private init; }
        public ReadOnlySpan<byte> Salt { get; private init; }
        public ReadOnlySpan<byte> Nonce { get; private init; }
        public ReadOnlySpan<byte> Tag { get; private init; }
        public ReadOnlySpan<byte> Ciphertext { get; private init; }

        public int Iterations => BinaryPrimitives.ReadUInt16LittleEndian(IterationsFactor) * IterationsFactorWeight;

        public static AesGcmCiphertextStr FromBytes(Span<byte> versionedEncryptedBytes)
        {
            var masterKeyIdEnd = 1;
            var masterKeyId = versionedEncryptedBytes[0];

            var iterationsFactorEnd = masterKeyIdEnd + IterationsFactorSize;
            var iterationsFactor = versionedEncryptedBytes[masterKeyIdEnd..iterationsFactorEnd];

            var saltEnd = iterationsFactorEnd + SaltSize;
            var salt = versionedEncryptedBytes[iterationsFactorEnd..saltEnd];

            var nonceEnd = saltEnd + NonceSize;
            var nonce = versionedEncryptedBytes[saltEnd..nonceEnd];

            var tagEnd = nonceEnd + TagSize;
            var tag = versionedEncryptedBytes[nonceEnd..tagEnd];

            var ciphertext = versionedEncryptedBytes[tagEnd..];

            return new AesGcmCiphertextStr
            {
                MasterKeyId = masterKeyId,
                IterationsFactor = iterationsFactor,
                Salt = salt,
                Nonce = nonce,
                Tag = tag,
                Ciphertext = ciphertext
            };
        }
    }

    private readonly ref struct FastCiphertextFrame
    {
        public byte MasterKeyId { get; private init; }
        public ReadOnlySpan<byte> Nonce { get; private init; }
        public ReadOnlySpan<byte> Tag { get; private init; }
        public ReadOnlySpan<byte> Ciphertext { get; private init; }

        public static FastCiphertextFrame FromBytes(Span<byte> versionedEncryptedBytes)
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

    private readonly ref struct Pbkdf2State
    {
        public ReadOnlySpan<byte> Salt { get; init; }
        public Span<byte> Output { get; init; }
        public int Iterations { get; init; }
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