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

    public static FileAesInputsV2 From(
        FullEncryptionSeed seed,
        FileEncryptionMetadata metadata)
    {
        if (seed.IkmKeyVersion != metadata.KeyVersion)
            throw new InvalidOperationException(
                $"Seed key version ({seed.IkmKeyVersion}) does not match " +
                $"metadata key version ({metadata.KeyVersion}).");

        var ikmChainStepSalts = seed.IkmChainStepSalts;
        var seedChainStepSalts = seed.ChainStepSalts;

        if (seedChainStepSalts.Count == 0)
            throw new InvalidOperationException(
                "Seed must contain at least one chain step salt.");

        var expectedMetadataCount = ikmChainStepSalts.Count + seedChainStepSalts.Count - 1;

        if (metadata.ChainStepSalts.Count != expectedMetadataCount)
            throw new InvalidOperationException(
                "Metadata chain step salts count does not match seed.");

        for (var i = 0; i < ikmChainStepSalts.Count; i++)
        {
            var areEqual = CryptographicOperations.FixedTimeEquals(
                metadata.ChainStepSalts[i],
                ikmChainStepSalts[i]);

            if (!areEqual)
            {
                throw new InvalidOperationException(
                    $"Metadata IKM chain step salt at index {i} does not match seed.");
            }
        }

        for (var i = 0; i < seedChainStepSalts.Count - 1; i++)
        {
            var areEqual = CryptographicOperations.FixedTimeEquals(
                metadata.ChainStepSalts[ikmChainStepSalts.Count + i],
                seedChainStepSalts[i]);

            if (!areEqual)
            {
                throw new InvalidOperationException(
                    $"Metadata chain step salt at index {ikmChainStepSalts.Count + i} does not match seed.");
            }
        }

        var isSaltEqual = CryptographicOperations.FixedTimeEquals(
            seedChainStepSalts[^1],
            metadata.Salt);

        if (!isSaltEqual)
        {
            throw new InvalidOperationException(
                "Last chain step salt of the seed does not match the metadata salt.");
        }

        var fileKey = new byte[Aes256GcmStreamingV2.DerivedKeySize];

        seed.Key.CopyTo(fileKey);

        return new FileAesInputsV2
        {
            FileKey = fileKey,
            KeyVersion = metadata.KeyVersion,
            ChainStepSalts = metadata.ChainStepSalts,
            Salt = metadata.Salt,
            NoncePrefix = metadata.NoncePrefix
        };
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