using System.Buffers;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Storages.Encryption;
using System.IO.Pipelines;
using PlikShare.Files.PreSignedLinks;
using Serilog;

namespace PlikShare.Storages;


public static class IStorageClientExtensions
{
    extension(IStorageClient client)
    {
        public StorageEncryptionType EncryptionType => client.Encryption.Type;

        public FileEncryptionMetadata? GenerateFileEncryptionMetadata(
            WorkspaceEncryptionMetadata? workspaceEncryption)
        {
            if (client.Encryption is NoStorageEncryption)
                return null;

            if (client.Encryption is ManagedStorageEncryption managed)
            {
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
                    KeyVersion = checked((byte) full.Details.LatestStorageDekVersion),
                    Salt = Aes256GcmStreamingV2.GenerateSalt(),
                    NoncePrefix = Aes256GcmStreamingV2.GenerateNoncePrefix(),
                    ChainStepSalts = [workspaceEncryption.Salt]
                };
            }

            throw new InvalidOperationException(
                $"Unsupported encryption type '{client.Encryption.Type}' " +
                $"for storage '{client.ExternalId}'.");
        }

        public async ValueTask<FilePartUploadResult> UploadFilePart(
            PipeReader input,
            UploadFilePartDetails uploadDetails,
            CancellationToken cancellationToken)
        {
            var heapBufferSize = FileEncryption.CalculateBufferSize(
                encryptionMode: uploadDetails.EncryptionMode,
                filePart: uploadDetails.Part);

            var heapBuffer = ArrayPool<byte>.Shared.Rent(
                minimumLength: heapBufferSize);

            var heapBufferMemory = heapBuffer
                .AsMemory()
                .Slice(0, heapBufferSize);

            try
            {
                await FileEncryption.CopyPlaintextIntoBuffer(
                    encryptionMode: uploadDetails.EncryptionMode,
                    input: input,
                    buffer: heapBufferMemory,
                    filePart: uploadDetails.Part,
                    cancellationToken: cancellationToken);

                return await client.UploadFilePart(
                    fileBytes: heapBufferMemory,
                    uploadDetails: uploadDetails,
                    cancellationToken: cancellationToken);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(heapBuffer);
            }
        }

        public async ValueTask<FilePartUploadResult> UploadFilePart(
            byte[] input,
            UploadFilePartDetails uploadDetails,
            CancellationToken cancellationToken)
        {
            if (uploadDetails.EncryptionMode is NoEncryption)
            {
                return await client.UploadFilePart(
                    fileBytes: input,
                    uploadDetails: uploadDetails,
                    cancellationToken: cancellationToken);
            }

            var heapBufferSize = FileEncryption.CalculateBufferSize(
                encryptionMode: uploadDetails.EncryptionMode,
                filePart: uploadDetails.Part);

            var heapBuffer = ArrayPool<byte>.Shared.Rent(
                minimumLength: heapBufferSize);

            var heapBufferMemory = heapBuffer
                .AsMemory()
                .Slice(0, heapBufferSize);

            try
            {
                FileEncryption.CopyPlaintextIntoBuffer(
                    encryptionMode: uploadDetails.EncryptionMode,
                    input: input,
                    buffer: heapBufferMemory,
                    filePart: uploadDetails.Part);

                return await client.UploadFilePart(
                    fileBytes: heapBufferMemory,
                    uploadDetails: uploadDetails,
                    cancellationToken: cancellationToken);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(heapBuffer);
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

                if (client.Encryption is not ManagedStorageEncryption managedStorageEncryption)
                    throw new InvalidOperationException(
                        $"Storage encryption is supposed to be {nameof(ManagedStorageEncryption)} " +
                        $"but found {client.Encryption.GetType()}");

                var ikm = managedStorageEncryption.GetEncryptionKey(
                    encryptionMetadata.KeyVersion);
                
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

        public int CalculateSafeBufferSizeForMultiFileUploads(
            int totalSizeInBytes,
            int numberOfFiles)
        {
            return client.Encryption switch
            {
                NoStorageEncryption => totalSizeInBytes,

                ManagedStorageEncryption => Aes256GcmStreamingV1.CalculateSafeBufferSizeForMultiFileUploads(
                    totalSizeInBytes,
                    numberOfFiles),

                FullStorageEncryption => Aes256GcmStreamingV2.CalculateSafeBufferSizeForMultiFileUploads(
                    totalSizeInBytes,
                    numberOfFiles),

                _ => throw new InvalidOperationException(
                    $"Unsupported storage encryption type '{client.Encryption.GetType().Name}'.")
            };
        }
    }
}