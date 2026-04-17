using PlikShare.Files.PreSignedLinks.RangeRequests;
using PlikShare.Files.Records;
using PlikShare.Storages.Encryption;
using PlikShare.Uploads.Cache;
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
        public int CalculateBufferSize(
            FilePart part)
        {
            if (metadata is null)
                return part.SizeInBytes;

            if (metadata.FormatVersion == 1)
                return Aes256GcmStreamingV1.CalculateEncryptedPartSize(
                    part);

            if (metadata.FormatVersion == 2)
                return Aes256GcmStreamingV2.CalculateEncryptedPartSize(
                    part,
                    metadata.ChainStepSalts.Count);

            throw new InvalidOperationException(
                $"Unsupported file encryption format version '{metadata.FormatVersion}'.");
        }

        public BytesRange CalculateFileRange(
            long fileSizeInBytes,
            BytesRange range)
        {
            if (metadata is null)
                return range;

            if (metadata.FormatVersion == 1)
            {
                var encryptedRange = Aes256GcmStreamingV1.EncryptedBytesRangeCalculator.FromUnencryptedRange(
                    unencryptedRange: range,
                    unencryptedFileSize: fileSizeInBytes);

                return encryptedRange.ToBytesRange();
            }

            if (metadata.FormatVersion == 2)
            {
                var encryptedRange = Aes256GcmStreamingV2.EncryptedBytesRangeCalculator.FromUnencryptedRange(
                    unencryptedRange: range,
                    unencryptedFileSize: fileSizeInBytes,
                    chainStepsCount: metadata.ChainStepSalts.Count);

                return encryptedRange.ToBytesRange();
            }

            throw new InvalidOperationException(
                $"Unsupported file encryption format version '{metadata.FormatVersion}'.");
        }
        
        public byte[]? ToIkm(
            WorkspaceEncryptionSession? workspaceEncryptionSession,
            IStorageClient storageClient)
        {
            if (metadata == null)
                return null;

            if (metadata is { FormatVersion: 1 })
            {
                if (storageClient.Encryption is not ManagedStorageEncryption managed)
                    throw new InvalidOperationException(
                        $"Cannot resolve IKM for V1 file: storage encryption must be Managed, " +
                        $"but was '{storageClient.Encryption.GetType().Name}'.");

                return managed.GetEncryptionKey(metadata.KeyVersion);
            }

            if (metadata is { FormatVersion: 2 })
            {
                if (workspaceEncryptionSession is null)
                    throw new InvalidOperationException(
                        "Cannot resolve IKM for V2 file: workspace encryption session is null.");

                return workspaceEncryptionSession.GetDekForVersion(metadata.KeyVersion);
            }

            throw new InvalidOperationException(
                $"Unsupported file encryption format version '{metadata.FormatVersion}'.");
        }

        public FileEncryptionMode ToEncryptionMode(
            WorkspaceEncryptionSession? workspaceEncryptionSession,
            IStorageClient storageClient)
        {
            if (metadata is null)
                return NoEncryption.Instance;
            
            var ikm = metadata.ToIkm(
                workspaceEncryptionSession: workspaceEncryptionSession,
                storageClient: storageClient);

            if (ikm is null)
                throw new InvalidOperationException(
                    $"Cannot build encryption mode for file with format version '{metadata.FormatVersion}' " +
                    $"because encryption IKM is null.");

            if (metadata.FormatVersion == 1)
                return new AesGcmV1Encryption(
                    Input: metadata.ToAesInputsV1(ikm));

            if (metadata.FormatVersion == 2)
                return new AesGcmV2Encryption(
                    Input: metadata.ToAesInputsV2(ikm));

            throw new InvalidOperationException(
                $"Unsupported file encryption format version '{metadata.FormatVersion}'.");
        }
    }

    extension(FileEncryptionMetadata metadata)
    {
        public FileAesInputsV1 ToAesInputsV1(byte[] ikm)
        {
            return new FileAesInputsV1(
                Ikm: ikm,
                KeyVersion: metadata.KeyVersion,
                Salt: metadata.Salt,
                NoncePrefix: metadata.NoncePrefix);
        }

        public FileAesInputsV2 ToAesInputsV2(byte[] ikm)
        {
            return new FileAesInputsV2(
                Ikm: ikm,
                KeyVersion: metadata.KeyVersion,
                ChainStepSalts: metadata.ChainStepSalts,
                Salt: metadata.Salt,
                NoncePrefix: metadata.NoncePrefix);
        }
    }
}