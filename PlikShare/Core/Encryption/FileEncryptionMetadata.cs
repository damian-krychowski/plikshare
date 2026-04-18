using PlikShare.Storages.Encryption;
using System.ComponentModel;
using PlikShare.Storages;

namespace PlikShare.Core.Encryption;

[ImmutableObject(true)]
public sealed class FileEncryptionMetadata
{
    public required byte FormatVersion { get; init; }
    public required byte KeyVersion { get; init; }
    public required byte[] Salt { get; init; }
    public required byte[] NoncePrefix { get; init; }

    /// <summary>
    /// Sequence of 32-byte salts that derive intermediate DEKs from the storage DEK
    /// down to the DEK used to derive this file's <see cref="Salt"/>-based file_key.
    /// Empty for legacy V1 files which derive file_key directly from the storage DEK.
    /// First step is typically the workspace salt; subsequent steps add box, user or link
    /// scope as the schema evolves.
    /// </summary>
    public required IReadOnlyList<byte[]> ChainStepSalts { get; init; }
}

public static class FileEncryptionMetadataExtensions
{
    extension(FileEncryptionMetadata? metadata)
    {
        public FileEncryptionMode ToEncryptionMode(
            WorkspaceEncryptionSession? workspaceEncryptionSession,
            IStorageClient storageClient)
        {
            if (metadata is null)
                return NoEncryption.Instance;
            
            if (metadata.FormatVersion == 1)
            {
                if (storageClient.Encryption is not ManagedStorageEncryption managed)
                    throw new InvalidOperationException(
                        $"Cannot resolve IKM for V1 file: storage encryption must be Managed, " +
                        $"but was '{storageClient.Encryption.GetType().Name}'.");

                return new AesGcmV1Encryption(
                    Input: new FileAesInputsV1(
                        Ikm: managed.GetEncryptionKey(metadata.KeyVersion),
                        KeyVersion: metadata.KeyVersion,
                        Salt: metadata.Salt,
                        NoncePrefix: metadata.NoncePrefix));
            }

            if (metadata is { FormatVersion: 2 })
            {
                if (workspaceEncryptionSession is null)
                    throw new InvalidOperationException(
                        "Cannot resolve IKM for V2 file: workspace encryption session is null.");

                return new AesGcmV2Encryption(
                    Input: new FileAesInputsV2(
                        Ikm: workspaceEncryptionSession.GetDekForVersion(
                            metadata.KeyVersion),
                        KeyVersion: metadata.KeyVersion,
                        ChainStepSalts: metadata.ChainStepSalts,
                        Salt: metadata.Salt,
                        NoncePrefix: metadata.NoncePrefix));
            }

            throw new InvalidOperationException(
                $"Unsupported file encryption format version '{metadata.FormatVersion}'.");
        }
    }
}