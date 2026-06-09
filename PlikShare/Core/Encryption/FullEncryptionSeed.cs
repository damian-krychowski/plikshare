using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using PlikShare.Storages.Encryption;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Core.Encryption;

public sealed class FullEncryptionSeed : IMetadataEncryptionSeed, IDisposable
{
    private int _disposed;

    public required byte IkmKeyVersion { get; init; }

    public required IReadOnlyList<byte[]> IkmChainStepSalts { get; init; }
    public required IReadOnlyList<byte[]> ChainStepSalts { get; init; }


    public required byte[] Key { get; init; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        CryptographicOperations.ZeroMemory(Key);
    }

    public static FullEncryptionSeed Prepare(
        SecureBytes ikm,
        IReadOnlyList<byte[]> ikmChainStepSalts,
        byte keyVersion)
    {
        var salt = RandomNumberGenerator.GetBytes(
            KeyDerivationChain.StepSaltSize);

        var key = new byte[KeyDerivationChain.DerivedKeySize];

        ikm.DeriveKey(
            chainStepSalts: salt,
            output: key);

        return new FullEncryptionSeed
        {
            IkmKeyVersion = keyVersion,
            IkmChainStepSalts = ikmChainStepSalts,
            ChainStepSalts = [salt],
            Key = key
        };
    }

    public static FullEncryptionSeed Prepare(
        WorkspaceContext workspace,
        WorkspaceEncryptionSession session)
    {
        if (workspace.EncryptionType != StorageEncryptionType.Full)
            throw new InvalidOperationException(
                $"Cannot create FullEncryptionSeed for workspace with encryption '{workspace.EncryptionType}'");

        if (workspace.Id != session.WorkspaceId)
            throw new InvalidOperationException(
                $"Workspace (Id: {workspace.Id}) does not match " +
                $"encryption session (WorkspaceId: {session.WorkspaceId})");

        var latest = session.GetLatestDek();

        return Prepare(
            ikm: latest.Dek,
            ikmChainStepSalts: [workspace.EncryptionMetadata!.Salt],
            keyVersion: (byte) latest.StorageDekVersion);
    }
    IMetadataEncryptionSeed IMetadataEncryptionSeed.DeriveNew() => DeriveNew();

    public FullEncryptionSeed DeriveNew()
    {
        var salt = RandomNumberGenerator.GetBytes(
            KeyDerivationChain.StepSaltSize);

        var newKey = new byte[KeyDerivationChain.DerivedKeySize];

        HKDF.DeriveKey(
            hashAlgorithmName: HashAlgorithmName.SHA256,
            ikm: Key,
            output: newKey,
            salt: salt,
            info: null);

        return new FullEncryptionSeed
        {
            IkmKeyVersion = IkmKeyVersion,
            Key = newKey,
            IkmChainStepSalts = IkmChainStepSalts,
            ChainStepSalts = [..ChainStepSalts, salt]
        };
    }

    public EncodedMetadataValue EncodeMetadata(string value)
    {
        if (value.StartsWith(AesGcmMetadataV1.ReservedPrefix, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Metadata value must not start with reserved prefix '{AesGcmMetadataV1.ReservedPrefix}'. " +
                "Request validation should have rejected this input before reaching the encryption layer.");

        return AesGcmMetadataV1.Encode(
            value: value,
            keyVersion: IkmKeyVersion,
            metadataKey: Key,
            chainStepSalts: ChainStepSalts);
    }
}


public sealed class FullEncryptionSeedEphemeral
{
    public required byte IkmKeyVersion { get; init; }
    public required byte[] IkmChainStepSalts { get; init; }
    public required byte[] ChainStepSalts { get; init; }
    
    public required EncodedEphemeralValue EncodedKey { get; init; }

    public static FullEncryptionSeedEphemeral Prepare(
        SecureBytes ikm,
        IReadOnlyList<byte[]> ikmChainStepSalts,
        byte keyVersion,
        EphemeralKeyRing ephemeralKeyRing)
    {
        var salt = RandomNumberGenerator.GetBytes(
            KeyDerivationChain.StepSaltSize);

        Span<byte> derivedKey = stackalloc byte[
            KeyDerivationChain.DerivedKeySize];

        try
        {
            ikm.DeriveKey(
                chainStepSalts: salt,
                output: derivedKey);

            return new FullEncryptionSeedEphemeral
            {
                IkmKeyVersion = keyVersion,
                
                IkmChainStepSalts = KeyDerivationChain.Serialize(
                    ikmChainStepSalts)!,

                ChainStepSalts = salt,

                EncodedKey = ephemeralKeyRing.Encode(
                    derivedKey)
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derivedKey);
        }
    }

    public static FullEncryptionSeedEphemeral Prepare(
        WorkspaceContext workspace,
        WorkspaceEncryptionSession session,
        EphemeralKeyRing ephemeralKeyRing)
    {
        if (workspace.EncryptionType != StorageEncryptionType.Full)
            throw new InvalidOperationException(
                $"Cannot create FullEncryptionSeed for workspace with encryption '{workspace.EncryptionType}'");

        if (workspace.Id != session.WorkspaceId)
            throw new InvalidOperationException(
                $"Workspace (Id: {workspace.Id}) does not match " +
                $"encryption session (WorkspaceId: {session.WorkspaceId})");

        var latest = session.GetLatestDek();

        return Prepare(
            ikm: latest.Dek,
            keyVersion: (byte)latest.StorageDekVersion,
            ikmChainStepSalts: [workspace.EncryptionMetadata!.Salt],
            ephemeralKeyRing: ephemeralKeyRing);
    }

    public static FullEncryptionSeedEphemeral FromFile(
        FileEncryptionMetadata fileEncryptionMetadata,
        WorkspaceContext workspace,
        WorkspaceEncryptionSession session,
        EphemeralKeyRing ephemeralKeyRing)
    {
        if (workspace.EncryptionType != StorageEncryptionType.Full)
            throw new InvalidOperationException(
                $"Cannot create FullEncryptionSeed for workspace with encryption '{workspace.EncryptionType}'");

        if (workspace.Id != session.WorkspaceId)
            throw new InvalidOperationException(
                $"Workspace (Id: {workspace.Id}) does not match " +
                $"encryption session (WorkspaceId: {session.WorkspaceId})");

        var dek = session.GetDekForVersion(
            fileEncryptionMetadata.KeyVersion);

        var fileChainStepSalts = fileEncryptionMetadata.ChainStepSalts;

        if (fileChainStepSalts.Count == 0)
            throw new InvalidOperationException(
                "File encryption metadata must contain at least one chain step salt.");

        var workspaceSalt = workspace.EncryptionMetadata!.Salt;

        if (!CryptographicOperations.FixedTimeEquals(fileChainStepSalts[0], workspaceSalt))
            throw new InvalidOperationException(
                "First chain step salt of the file does not match the workspace salt.");

        IReadOnlyList<byte[]> chainStepSalts =
        [
            ..fileChainStepSalts.Skip(1),
            fileEncryptionMetadata.Salt
        ];

        Span<byte> derivedKey = stackalloc byte[
            KeyDerivationChain.DerivedKeySize];

        try
        {
            dek.DeriveKey(chainStepSalts, derivedKey);

            return new FullEncryptionSeedEphemeral
            {
                IkmKeyVersion = fileEncryptionMetadata.KeyVersion,

                IkmChainStepSalts = KeyDerivationChain.Serialize(
                    [workspaceSalt])!,

                ChainStepSalts = KeyDerivationChain.Serialize(
                    chainStepSalts)!,

                EncodedKey = ephemeralKeyRing.Encode(
                    derivedKey)
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derivedKey);
        }
    }

    public EphemeralDecodeStatus TryDecode(
        EphemeralKeyRing ephemeralKeyRing,
        out FullEncryptionSeed? fullEncryptionSeed)
    {
        var result = ephemeralKeyRing.TryDecode(
            EncodedKey, 
            out byte[] key);

        if (result != EphemeralDecodeStatus.Ok)
        {
            fullEncryptionSeed = null;
            return result;
        }

        fullEncryptionSeed = new FullEncryptionSeed
        {
            Key = key,

            IkmKeyVersion = IkmKeyVersion,

            IkmChainStepSalts = KeyDerivationChain.Deserialize(
                IkmChainStepSalts),

            ChainStepSalts = KeyDerivationChain.Deserialize(
                ChainStepSalts)
        };

        return EphemeralDecodeStatus.Ok;
    }

    public const string SerializedFormatVersion = "1";

    public string Serialize()
    {
        return string.Join(
            '.',
            SerializedFormatVersion,
            IkmKeyVersion.ToString(CultureInfo.InvariantCulture),
            Base64Url.EncodeToString(IkmChainStepSalts),
            Base64Url.EncodeToString(ChainStepSalts),
            EncodedKey.Encoded);
    }

    public static FullEncryptionSeedEphemeral Deserialize(string serialized)
    {
        ArgumentException.ThrowIfNullOrEmpty(serialized);

        var parts = serialized.Split('.', 5);

        if (parts.Length != 5)
            throw new FormatException(
                $"Invalid {nameof(FullEncryptionSeedEphemeral)} format.");

        if (parts[0] != SerializedFormatVersion)
            throw new FormatException(
                $"Unsupported {nameof(FullEncryptionSeedEphemeral)} format version '{parts[0]}'.");

        return new FullEncryptionSeedEphemeral
        {
            IkmKeyVersion = byte.Parse(
                parts[1],
                CultureInfo.InvariantCulture),

            IkmChainStepSalts = Base64Url.DecodeFromChars(parts[2]),

            ChainStepSalts = Base64Url.DecodeFromChars(parts[3]),

            EncodedKey = new EncodedEphemeralValue(parts[4])
        };
    }
}

public static class FullEncryptionSeedExtensions
{
    extension(FullEncryptionSeed seed)
    {
        public FileEncryptionMode ToFileEncryptionMode(
            FileEncryptionMetadata metadata)
        {
            var fileAesInputsV2 = FileAesInputsV2.From(
                seed: seed!,
                metadata: metadata);

            var fullEncryptionMode = new AesGcmV2Encryption(
                Input: fileAesInputsV2);

            return fullEncryptionMode;
        }

        public FileEncryptionMetadata GenerateFileEncryptionMetadata()
        {
            return new FileEncryptionMetadata
            {
                FormatVersion = 2,
                KeyVersion = seed.IkmKeyVersion,
                Salt = seed.ChainStepSalts.Last(),
                ChainStepSalts =
                [
                    ..seed.IkmChainStepSalts,
                    ..seed.ChainStepSalts.SkipLast(1)
                ],
                NoncePrefix = Aes256GcmStreamingV2.GenerateNoncePrefix()
            };
        }
    }
}