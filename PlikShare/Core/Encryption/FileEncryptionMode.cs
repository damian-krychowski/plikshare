using System.Security.Cryptography;

namespace PlikShare.Core.Encryption;

public abstract record FileEncryptionMode;

public sealed record NoEncryption : FileEncryptionMode
{
    public static NoEncryption Instance { get; } = new();
}

public sealed record AesGcmV1Encryption(
    FileAesInputsV1 Input) : FileEncryptionMode;

public sealed record AesGcmV2Encryption(
    FileAesInputsV2 Input) : FileEncryptionMode;


public sealed record FileAesInputsV1(
    byte[] Ikm,
    byte KeyVersion,
    byte[] Salt,
    byte[] NoncePrefix);

public sealed class FileAesInputsV2 : IDisposable
{
    private int _disposed;

    public required byte[] FileKey { get; init; }
    public required byte KeyVersion { get; init; }
    public required IReadOnlyList<byte[]> ChainStepSalts { get; init; }
    public required byte[] Salt { get; init; }
    public required byte[] NoncePrefix { get; init; }

    public void Deconstruct(
        out byte[] fileKey,
        out byte keyVersion,
        out IReadOnlyList<byte[]> chainStepSalts,
        out byte[] salt,
        out byte[] noncePrefix)
    {
        fileKey = FileKey;
        keyVersion = KeyVersion;
        chainStepSalts = ChainStepSalts;
        salt = Salt;
        noncePrefix = NoncePrefix;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        CryptographicOperations.ZeroMemory(FileKey);
    }

    public static FileAesInputsV2 Prepare(
        SecureBytes ikm,
        FileEncryptionMetadata metadata)
    {
        var fileKey = new byte[Aes256GcmStreamingV2.DerivedKeySize];

        ikm.DeriveKey(
            chainStepSalts: metadata.Salt,
            output: fileKey);
        
        return new FileAesInputsV2
        {
            FileKey = fileKey,
            KeyVersion = metadata.KeyVersion,
            ChainStepSalts = metadata.ChainStepSalts,
            Salt = metadata.Salt,
            NoncePrefix = metadata.NoncePrefix
        };
    }

    public static FileAesInputsV2 Prepare(
        FileAesInputsV2Wire wire,
        IMasterDataEncryption masterEncryption)
    {
        var fileKey = new byte[Aes256GcmStreamingV2.DerivedKeySize];

        masterEncryption.DecryptBytes(
            versionedEncryptedBytes: wire.EncryptedFileKey,
            destination: fileKey);

        return new FileAesInputsV2
        {
            FileKey = fileKey,
            KeyVersion = wire.KeyVersion,
            ChainStepSalts = wire.ChainStepSalts,
            Salt = wire.Salt,
            NoncePrefix = wire.NoncePrefix
        };
    }

    public FileEncryptionMetadata ToMetadata() => new()
    {
        FormatVersion = 2,
        Salt = Salt,
        KeyVersion = KeyVersion,
        ChainStepSalts = ChainStepSalts,
        NoncePrefix = NoncePrefix
    };
}

public sealed class FileAesInputsV2Wire
{
    public required byte[] EncryptedFileKey { get; init; }

    public required byte KeyVersion { get; init; }
    public required IReadOnlyList<byte[]> ChainStepSalts { get; init; }
    public required byte[] Salt { get; init; }
    public required byte[] NoncePrefix { get; init; }

    public static FileAesInputsV2Wire Prepare(
        SecureBytes ikm,
        FileEncryptionMetadata metadata,
        IMasterDataEncryption masterEncryption)
    {
        // The whole point of the wire form is to avoid the mlock/pin overhead of SecureBytes
        // for cache entries. So plaintext lives ONLY on this stack frame: HKDF writes it,
        // master encryption reads it, the finally zeroes it. No heap, no SecureBytes alloc.
        // 32 bytes — well within any sane stack budget.
        Span<byte> fileKey = stackalloc byte[Aes256GcmStreamingV2.DerivedKeySize];

        try
        {
            ikm.DeriveKey(
                chainStepSalts: metadata.Salt,
                output: fileKey);

            return new FileAesInputsV2Wire
            {
                EncryptedFileKey = masterEncryption.EncryptBytes(fileKey),
                KeyVersion = metadata.KeyVersion,
                ChainStepSalts = metadata.ChainStepSalts,
                Salt = metadata.Salt,
                NoncePrefix = metadata.NoncePrefix
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(fileKey);
        }
    }
}

public static class FileEncryptionModeExtensions
{
    extension(FileEncryptionMode mode)
    {
        public string FormatVersion
        {
            get
            {
                return mode switch
                {
                    NoEncryption => "None",
                    AesGcmV1Encryption => "V1",
                    AesGcmV2Encryption => "V2",
                    _ => throw new ArgumentOutOfRangeException(nameof(mode))
                };
            }
        }
    }
}