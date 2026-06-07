using PlikShare.BulkDownload;
using PlikShare.Core.Encryption;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Storages;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.FileReading;
using System.IO.Pipelines;
using System.Security.Cryptography;

namespace PlikShare.Workspaces.Cache;

public static class WorkspaceContextExtensions
{
    extension(WorkspaceContext workspace)
    {
        public StorageEncryptionType EncryptionType => workspace.Storage.Encryption.Type;
        
        public FileKey GenerateFileKey()
        {
            return workspace.Storage.GenerateFileKey();
        }

        public string GenerateFileKeySecretPart()
        {
            return workspace.Storage.GenerateFileKeySecretPart();
        }

        public EncryptableMetadata ToEncryptableMetadata(
            string value,
            WorkspaceEncryptionSession? workspaceEncryptionSession)
        {
            if (value.StartsWith(AesGcmMetadataV1.ReservedPrefix, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Metadata value must not start with reserved prefix '{AesGcmMetadataV1.ReservedPrefix}'. " +
                    "Request validation should have rejected this input before reaching the encryption layer.");

            var encryption = workspace.Storage.Encryption;

            if (encryption is NoStorageEncryption or ManagedStorageEncryption)
            {
                if (workspaceEncryptionSession is not null)
                    throw new InvalidOperationException(
                        $"WorkspaceEncryptionSession must be null for '{encryption.GetType().Name}' " +
                        $"storage '{workspace.Storage.ExternalId}' — metadata is not encrypted at rest for this mode.");

                return NoMetadataEncryption.Prepare(
                    value: value);
            }

            if (encryption is FullStorageEncryption)
            {
                if (workspaceEncryptionSession is null)
                    throw new InvalidOperationException(
                        $"WorkspaceEncryptionSession is required for full-encrypted storage " +
                        $"'{workspace.Storage.ExternalId}' to encrypt metadata.");

                var latest = workspaceEncryptionSession.GetLatestDek();

                var input = MetadataAesInputsV1.Prepare(
                    ikm: latest.Dek,
                    keyVersion: (byte)latest.StorageDekVersion,
                    chainStepSalts:
                    [
                        RandomNumberGenerator.GetBytes(KeyDerivationChain.StepSaltSize)
                    ]);

                return new EncryptableMetadata(
                    Value: value,
                    EncryptionMode: new AesGcmMetadataV1Encryption(
                        Input: input));
            }

            throw new InvalidOperationException(
                $"Unsupported encryption type '{encryption.Type}' " +
                $"for storage '{workspace.Storage.ExternalId}'.");
        }

        public string DecodeMetadata(
            EncodedMetadataValue encodedValue,
            WorkspaceEncryptionSession? workspaceEncryptionSession)
        {
            var encryption = workspace.Storage.Encryption;

            if (encryption is NoStorageEncryption or ManagedStorageEncryption)
            {
                if (workspaceEncryptionSession is not null)
                    throw new InvalidOperationException(
                        $"WorkspaceEncryptionSession must be null for '{encryption.GetType().Name}' " +
                        $"storage '{workspace.Storage.ExternalId}' — metadata is not encrypted at rest for this mode.");

                if (encodedValue.Encoded.StartsWith(AesGcmMetadataV1.ReservedPrefix, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"Metadata value starts with reserved prefix '{AesGcmMetadataV1.ReservedPrefix}' " +
                        $"but storage '{workspace.Storage.ExternalId}' uses '{encryption.GetType().Name}' — " +
                        "encrypted value encountered for non-encrypted storage mode.");

                return encodedValue.Encoded;
            }

            if (encryption is FullStorageEncryption)
            {
                if (workspaceEncryptionSession is null)
                    throw new InvalidOperationException(
                        $"WorkspaceEncryptionSession is required for full-encrypted storage " +
                        $"'{workspace.Storage.ExternalId}' to decrypt metadata.");

                if (!encodedValue.Encoded.StartsWith(AesGcmMetadataV1.ReservedPrefix, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"Metadata value does not start with reserved prefix '{AesGcmMetadataV1.ReservedPrefix}' " +
                        $"for full-encrypted storage '{workspace.Storage.ExternalId}' — value is not encrypted.");

                return AesGcmMetadataV1.Decode(
                    encodedValue.Encoded,
                    workspaceEncryptionSession);
            }

            throw new InvalidOperationException(
                $"Unsupported encryption type '{encryption.Type}' " +
                $"for storage '{workspace.Storage.ExternalId}'.");
        }

        public EncodedMetadataValue EncodeMetadata(
            string value,
            WorkspaceEncryptionSession? workspaceEncryptionSession)
        {
            if (value.StartsWith(AesGcmMetadataV1.ReservedPrefix, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Metadata value must not start with reserved prefix '{AesGcmMetadataV1.ReservedPrefix}'. " +
                    "Request validation should have rejected this input before reaching the encryption layer.");

            var encryption = workspace.Storage.Encryption;

            if (encryption is NoStorageEncryption or ManagedStorageEncryption)
            {
                if (workspaceEncryptionSession is not null)
                    throw new InvalidOperationException(
                        $"WorkspaceEncryptionSession must be null for '{encryption.GetType().Name}' " +
                        $"storage '{workspace.Storage.ExternalId}' — metadata is not encrypted at rest for this mode.");

                return new EncodedMetadataValue(value);
            }

            if (encryption is FullStorageEncryption)
            {
                if (workspaceEncryptionSession is null)
                    throw new InvalidOperationException(
                        $"WorkspaceEncryptionSession is required for full-encrypted storage " +
                        $"'{workspace.Storage.ExternalId}' to encrypt metadata.");

                var latest = workspaceEncryptionSession.GetLatestDek();

                var input = MetadataAesInputsV1.Prepare(
                    ikm: latest.Dek,
                    keyVersion: (byte)latest.StorageDekVersion,
                    chainStepSalts:
                    [
                        RandomNumberGenerator.GetBytes(KeyDerivationChain.StepSaltSize)
                    ]);

                var encoded = AesGcmMetadataV1.Encode(
                    value: value,
                    aesInput: input);

                return new EncodedMetadataValue(encoded);
            }

            throw new InvalidOperationException(
                $"Unsupported encryption type '{encryption.Type}' " +
                $"for storage '{workspace.Storage.ExternalId}'.");
        }

        public FullEncryptionSeedEphemeral? TryGetFileEncryptionSeed(
            FileEncryptionMetadata? encryptionMetadata,
            WorkspaceEncryptionSession? workspaceEncryptionSession,
            EphemeralKeyRing ephemeralKeyRing)
        {
            var encryption = workspace.Storage.Encryption;

            if (encryption is NoStorageEncryption or ManagedStorageEncryption)
            {
                if (workspaceEncryptionSession is not null)
                    throw new InvalidOperationException(
                        $"WorkspaceEncryptionSession must be null for '{encryption.GetType().Name}' " +
                        $"storage '{workspace.Storage.ExternalId}' — files are not encrypted at rest for this mode.");

                return null;
            }

            if (encryption is FullStorageEncryption)
            {
                if (workspaceEncryptionSession is null)
                    throw new InvalidOperationException(
                        $"WorkspaceEncryptionSession is required for full-encrypted storage " +
                        $"'{workspace.Storage.ExternalId}' to derive the file encryption seed.");

                if (encryptionMetadata is null)
                    throw new InvalidOperationException(
                        $"FileEncryptionMetadata is required for full-encrypted storage " +
                        $"'{workspace.Storage.ExternalId}' to derive the file encryption seed.");

                return FullEncryptionSeedEphemeral.FromFile(
                    fileEncryptionMetadata: encryptionMetadata,
                    workspace: workspace,
                    session: workspaceEncryptionSession,
                    ephemeralKeyRing: ephemeralKeyRing);
            }

            throw new InvalidOperationException(
                $"Unsupported encryption type '{encryption.Type}' " +
                $"for storage '{workspace.Storage.ExternalId}'.");
        }

        public FileEncryptionMetadata? GenerateFileEncryptionMetadata()
        {
            var client = workspace.Storage;
            var workspaceEncryption = workspace.EncryptionMetadata;

            if (client.Encryption is NoStorageEncryption)
            {
                if (workspaceEncryption is not null)
                    throw new InvalidOperationException(
                        $"WorkspaceEncryptionMetadata must not be provided for unencrypted " +
                        $"storage '{client.ExternalId}' — there is no key derivation path to record.");

                return null;
            }

            if (client.Encryption is ManagedStorageEncryption managed)
            {
                if (workspaceEncryption is not null)
                    throw new InvalidOperationException(
                        $"WorkspaceEncryptionMetadata must not be provided for managed-encrypted " +
                        $"storage '{client.ExternalId}' — V1 derives its key from the managed key " +
                        $"version alone and records no chain-step salts.");

                return new FileEncryptionMetadata
                {
                    FormatVersion = 1,
                    KeyVersion = managed.LatestKeyVersion,
                    Salt = Aes256GcmStreamingV1.GenerateSalt(),
                    NoncePrefix = Aes256GcmStreamingV1.GenerateNoncePrefix(),
                    ChainStepSalts = []
                };
            }

            if (client.Encryption is FullStorageEncryption full)
            {
                if (workspaceEncryption is null)
                    throw new InvalidOperationException(
                        $"WorkspaceEncryptionMetadata is required to generate file metadata for " +
                        $"full-encrypted storage '{client.ExternalId}' — the workspace salt " +
                        $"must be recorded in the file header's chain-step salts.");

                return new FileEncryptionMetadata
                {
                    FormatVersion = 2,
                    KeyVersion = checked((byte)full.Details.LatestStorageDekVersion),
                    Salt = Aes256GcmStreamingV2.GenerateSalt(),
                    NoncePrefix = Aes256GcmStreamingV2.GenerateNoncePrefix(),
                    ChainStepSalts = [workspaceEncryption.Salt]
                };
            }

            throw new InvalidOperationException(
                $"Unsupported encryption type '{client.Encryption.Type}' " +
                $"for storage '{client.ExternalId}'.");
        }

        public FileEncryptionMode GetFileEncryptionMode(
            FileEncryptionMetadata? fileEncryptionMetadata,
            WorkspaceEncryptionSession? workspaceEncryptionSession)
        {
            var encryption = workspace.Storage.Encryption;

            if (encryption is NoStorageEncryption)
            {
                if (fileEncryptionMetadata is not null)
                    throw new InvalidOperationException(
                        $"FileEncryptionMetadata must be null for unencrypted storage " +
                        $"'{workspace.Storage.ExternalId}', but a V{fileEncryptionMetadata.FormatVersion} header was provided.");

                if (workspaceEncryptionSession is not null)
                    throw new InvalidOperationException(
                        $"WorkspaceEncryptionSession must be null for unencrypted storage " +
                        $"'{workspace.Storage.ExternalId}'.");

                return NoEncryption.Instance;
            }

            if (encryption is ManagedStorageEncryption managed)
            {
                if (fileEncryptionMetadata is null)
                    throw new InvalidOperationException(
                        $"FileEncryptionMetadata is required for managed-encrypted storage " +
                        $"'{workspace.Storage.ExternalId}'.");

                if (fileEncryptionMetadata.FormatVersion != 1)
                    throw new InvalidOperationException(
                        $"Managed-encrypted storage '{workspace.Storage.ExternalId}' requires a V1 file " +
                        $"header, but a V{fileEncryptionMetadata.FormatVersion} header was provided.");

                if (workspaceEncryptionSession is not null)
                    throw new InvalidOperationException(
                        $"WorkspaceEncryptionSession must be null for managed-encrypted storage " +
                        $"'{workspace.Storage.ExternalId}' — V1 derives its IKM from the managed key version alone.");

                return new AesGcmV1Encryption(
                    Input: new FileAesInputsV1(
                        Ikm: managed.GetEncryptionKey(fileEncryptionMetadata.KeyVersion),
                        KeyVersion: fileEncryptionMetadata.KeyVersion,
                        Salt: fileEncryptionMetadata.Salt,
                        NoncePrefix: fileEncryptionMetadata.NoncePrefix));
            }

            if (encryption is FullStorageEncryption)
            {
                if (fileEncryptionMetadata is null)
                    throw new InvalidOperationException(
                        $"FileEncryptionMetadata is required for full-encrypted storage " +
                        $"'{workspace.Storage.ExternalId}'.");

                if (fileEncryptionMetadata.FormatVersion != 2)
                    throw new InvalidOperationException(
                        $"Full-encrypted storage '{workspace.Storage.ExternalId}' requires a V2 file " +
                        $"header, but a V{fileEncryptionMetadata.FormatVersion} header was provided.");

                if (workspaceEncryptionSession is null)
                    throw new InvalidOperationException(
                        $"WorkspaceEncryptionSession is required for full-encrypted storage " +
                        $"'{workspace.Storage.ExternalId}' to resolve the V2 IKM.");

                var fileAesInputs = FileAesInputsV2.Prepare(
                    ikm: workspaceEncryptionSession.GetDekForVersion(
                        fileEncryptionMetadata.KeyVersion),
                    metadata: fileEncryptionMetadata);

                return new AesGcmV2Encryption(
                    Input: fileAesInputs);
            }

            throw new InvalidOperationException(
                $"Unsupported encryption type '{encryption.Type}' " +
                $"for storage '{workspace.Storage.ExternalId}'.");
        }

        public ValueTask<IStorageFile> DownloadFile(
            DownloadFileDetails fileDetails,
            CancellationToken cancellationToken)
        {
            return workspace.Storage.DownloadFile(
                fileDetails: fileDetails,
                bucketName: workspace.BucketName,
                cancellationToken: cancellationToken);
        }

        public ValueTask<IStorageFile> DownloadFileRange(
            DownloadFileRangeDetails fileDetails,
            CancellationToken cancellationToken)
        {
            return workspace.Storage.DownloadFileRange(
                fileDetails: fileDetails,
                bucketName: workspace.BucketName,
                cancellationToken: cancellationToken);
        }

        public Task DownloadFilesInBulk(
            BulkDownloadDetails bulkDownloadDetails,
            PipeWriter responsePipeWriter,
            CancellationToken cancellationToken)
        {
            return workspace.Storage.DownloadFilesInBulk(
                bulkDownloadDetails, 
                workspace.BucketName, 
                responsePipeWriter, 
                cancellationToken);
        }

        public ValueTask<FilePartUploadResult> UploadFilePart(
            PipeReader input,
            UploadFilePartDetails uploadDetails,
            CancellationToken cancellationToken)
        {
            return workspace.Storage.UploadFilePart(
                input: input, 
                uploadDetails: uploadDetails, 
                bucketName: workspace.BucketName, 
                cancellationToken: cancellationToken);
        }

        public ValueTask<FilePartUploadResult> UploadFilePart(
            byte[] input,
            UploadFilePartDetails uploadDetails,
            CancellationToken cancellationToken)
        {
            return workspace.Storage.UploadFilePart(
                input: input,
                uploadDetails: uploadDetails, 
                bucketName: workspace.BucketName, 
                cancellationToken: cancellationToken);
        }

        public ValueTask<FilePartUploadResult> UploadFilePart(
            Memory<byte> input,
            UploadFilePartDetails uploadDetails,
            CancellationToken cancellationToken)
        {
            return workspace.Storage.UploadFilePart(
                input: input,
                uploadDetails: uploadDetails,
                bucketName: workspace.BucketName,
                cancellationToken: cancellationToken);
        }

        public async ValueTask ReadRange(
            DownloadFileRangeDetails details,
            PipeWriter output,
            CancellationToken cancellationToken)
        {
            await using var storageFile = await workspace.Storage.DownloadFileRange(
                fileDetails: details,
                bucketName: workspace.BucketName,
                cancellationToken: cancellationToken);

            await storageFile.ReadTo(
                output,
                cancellationToken);
        }
    }
}