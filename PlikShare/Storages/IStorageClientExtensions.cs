using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Files.Records;
using PlikShare.Storages.Encryption;
using System.IO.Pipelines;
using Serilog;

namespace PlikShare.Storages;

public static class IStorageClientExtensions
{
    extension(IStorageClient client)
    {
        public FileEncryptionMetadata? GenerateFileEncryptionMetadata(
            WorkspaceEncryptionMetadata? workspaceEncryption)
        {
            if (client.EncryptionType == StorageEncryptionType.None)
                return null;

            if (client.EncryptionType == StorageEncryptionType.Managed)
            {
                return new FileEncryptionMetadata
                {
                    FormatVersion = 1,
                    KeyVersion = client.ManagedEncryptionKeyProvider!.GetLatestKeyVersion(),
                    Salt = Aes256GcmStreamingV1.GenerateSalt(),
                    NoncePrefix = Aes256GcmStreamingV1.GenerateNoncePrefix(),
                    ChainStepSalts = []
                };
            }

            if (client.EncryptionType == StorageEncryptionType.Full)
            {
                var fullDetails = client.EncryptionDetails?.Full
                    ?? throw new InvalidOperationException(
                        $"Storage '{client.ExternalId}' has EncryptionType=Full but no Full encryption details.");

                // Chain salts record the derivation path a recovery tool must walk from the
                // recovery seed to the IKM that V2 actually uses (the Workspace DEK). For a
                // full-encrypted workspace that path is one HKDF step:
                //   Storage DEK v N  --HKDF(workspace_salt)-->  Workspace DEK
                // The runtime encryption path ignores these salts (V2 takes IKM = Workspace
                // DEK directly); they only matter when offline recovery reconstructs the
                // Workspace DEK from the seed + file header alone, with no DB lookup.
                if (workspaceEncryption is null)
                    throw new InvalidOperationException(
                        $"WorkspaceEncryptionMetadata is required to generate file metadata for " +
                        $"full-encrypted storage '{client.ExternalId}' — the workspace salt " +
                        $"must be recorded in the file header's chain-step salts.");

                return new FileEncryptionMetadata
                {
                    FormatVersion = 2,
                    KeyVersion = checked((byte)fullDetails.LatestStorageDekVersion),
                    Salt = Aes256GcmStreamingV2.GenerateSalt(),
                    NoncePrefix = Aes256GcmStreamingV2.GenerateNoncePrefix(),
                    ChainStepSalts = [workspaceEncryption.Salt]
                };
            }

            throw new InvalidOperationException(
                $"Unsupported encryption type '{client.EncryptionType}' " +
                $"for storage '{client.ExternalId}'.");
        }

        public void PrepareFilePartUploadBuffer(
            Memory<byte> buffer,
            long fileSizeInBytes, 
            FilePart filePart,
            FileEncryptionMetadata? encryptionMetadata,
            WorkspaceEncryptionSession? workspaceEncryptionSession,
            CancellationToken cancellationToken)
        {
            if (encryptionMetadata is null)
                return;

            if (encryptionMetadata.FormatVersion == 1)
            {
                var ikm = client
                    .ManagedEncryptionKeyProvider
                    !.GetEncryptionKey(encryptionMetadata.KeyVersion);

                Aes256GcmStreamingV1.EncryptFilePartInPlace(
                    fileAesInputs: encryptionMetadata.ToAesInputsV1(ikm),
                    filePart: filePart,
                    fullFileSizeInBytes: fileSizeInBytes,
                    inputOutputBuffer: buffer,
                    cancellationToken: cancellationToken);
            }
            else if (encryptionMetadata.FormatVersion == 2)
            {
                Aes256GcmStreamingV2.EncryptFilePartInPlace(
                    fileAesInputs: encryptionMetadata.ToAesInputsV2(
                        ikm: workspaceEncryptionSession!.GetDekForVersion(encryptionMetadata.KeyVersion)),
                    filePart: filePart,
                    fullFileSizeInBytes: fileSizeInBytes,
                    inputOutputBuffer: buffer,
                    cancellationToken: cancellationToken);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unsupported file encryption format version '{encryptionMetadata.FormatVersion}'.");
            }
        }

        public async ValueTask WriteFileTo(
            Stream stream,
            PipeWriter output,
            long fileSizeInBytes,
            FileEncryptionMetadata? encryptionMetadata,
            WorkspaceEncryptionSession? workspaceEncryptionSession,
            CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;

            if (encryptionMetadata is null)
            {
                Log.Debug("Starting unencrypted file transfer");

                await stream.CopyToAsync(
                    destination: output,
                    cancellationToken: cancellationToken);

                var streamDuration = DateTime.UtcNow - startTime;
                var streamSpeed = fileSizeInBytes / Math.Max(1, streamDuration.TotalSeconds);

                Log.Debug(
                    "Completed unencrypted file transfer: {FileSize:N0} bytes in {DurationMs}ms. Speed: {Speed:N2} MB/s",
                    fileSizeInBytes,
                    streamDuration.TotalMilliseconds,
                    streamSpeed / 1024.0 / 1024.0);
            }
            else if (encryptionMetadata.FormatVersion == 1)
            {
                Log.Debug("Starting encrypted file transfer using AES-256-GCM");

                var ikm = client
                    .ManagedEncryptionKeyProvider
                    !.GetEncryptionKey(encryptionMetadata.KeyVersion);

                await Aes256GcmStreamingV1.Decrypt(
                    fileAesInputs: encryptionMetadata.ToAesInputsV1(ikm),
                    fileSizeInBytes: fileSizeInBytes,
                    input: PipeReader.Create(
                        stream,
                        new StreamPipeReaderOptions(
                            bufferSize: PlikShareStreams.DefaultBufferSize,
                            leaveOpen: false)),
                    output: output,
                    cancellationToken);

                var decryptDuration = DateTime.UtcNow - startTime;
                var decryptSpeed = fileSizeInBytes / Math.Max(1, decryptDuration.TotalSeconds);

                Log.Debug(
                    "Completed encrypted file transfer: {FileSize:N0} bytes in {DurationMs}ms. Speed: {Speed:N2} MB/s",
                    fileSizeInBytes,
                    decryptDuration.TotalMilliseconds,
                    decryptSpeed / 1024.0 / 1024.0);
            }
            else if (encryptionMetadata.FormatVersion == 2)
            {
                Log.Debug("Starting encrypted file transfer using AES-256-GCM");

                var ikm = workspaceEncryptionSession!.GetDekForVersion(
                    encryptionMetadata.KeyVersion);

                await Aes256GcmStreamingV2.Decrypt(
                    fileAesInputs: encryptionMetadata.ToAesInputsV2(ikm),
                    fileSizeInBytes: fileSizeInBytes,
                    input: PipeReader.Create(
                        stream,
                        new StreamPipeReaderOptions(
                            bufferSize: PlikShareStreams.DefaultBufferSize,
                            leaveOpen: false)),
                    output: output,
                    cancellationToken);

                var decryptDuration = DateTime.UtcNow - startTime;
                var decryptSpeed = fileSizeInBytes / Math.Max(1, decryptDuration.TotalSeconds);

                Log.Debug(
                    "Completed encrypted file transfer: {FileSize:N0} bytes in {DurationMs}ms. Speed: {Speed:N2} MB/s",
                    fileSizeInBytes,
                    decryptDuration.TotalMilliseconds,
                    decryptSpeed / 1024.0 / 1024.0);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unsupported file encryption format version '{encryptionMetadata.FormatVersion}'");
            }
        }
    }
}