using PlikShare.BulkDownload;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Storages.Encryption;
using Serilog.Events;
using System.IO.Compression;
using System.IO.Pipelines;
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

        public async Task DownloadFilesInBulk(
            BulkDownloadDetails bulkDownloadDetails,
            string bucketName,
            PipeWriter responsePipeWriter,
            CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            var totalFiles = bulkDownloadDetails.Files.Count;
            var processedFiles = 0;
            var failedFiles = 0;
            var totalBytes = 0L;

            Log.Information(
                "Starting bulk download operation for {FileCount} files from bucket {BucketName}",
                totalFiles,
                bucketName);

            var (files, folderSubtree) = bulkDownloadDetails;

            var uniqueFileNames = new UniqueFileNames(
                capacity: totalFiles);

            try
            {
                await using var archive = new ZipArchive(
                    stream: responsePipeWriter.AsStream(),
                    mode: ZipArchiveMode.Create,
                    leaveOpen: true);

                foreach (var file in files.OrderBy(f => folderSubtree.GetLevelInTree(f.FolderId)))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var fileStartTime = DateTime.UtcNow;

                    var s3FileKey = new S3FileKey
                    {
                        FileExternalId = file.ExternalId,
                        S3KeySecretPart = file.S3KeySecretPart
                    };

                    var folderPath = folderSubtree.GetPath(
                        folderId: file.FolderId);

                    try
                    {
                        Log.Debug(
                            "Processing file {FileNumber}/{TotalFiles}: {FileName} at path {FilePath}",
                            processedFiles + 1,
                            totalFiles,
                            file.FullName,
                            folderPath);

                        var (entryName, wasNameCollisionDetected) = uniqueFileNames.EnsureUniqueFileName(
                            fullFileName: file.FullName,
                            folderPath: folderPath);

                        if (wasNameCollisionDetected)
                        {
                            Log.Debug(
                                "File name collision detected. Original: {FilePath}{FileName}, Generated unique name: {UniqueName}",
                                folderPath, file.FullName, entryName);
                        }

                        var entry = archive.CreateEntry(
                            entryName,
                            CompressionLevel.NoCompression);

                        Log.Debug(
                            "Downloading file {FileExternalId} from {BucketName}/{S3FileKey}",
                            file.ExternalId,
                            bucketName,
                            s3FileKey.Value);

                        await using var storageFile = await client.DownloadFile(
                            fileDetails: new DownloadFileDetails(
                                S3FileKey: s3FileKey,
                                FileSizeInBytes: file.SizeInBytes,
                                EncryptionMode: file.EncryptionMode),
                            bucketName: bucketName,
                            cancellationToken: cancellationToken);
                        
                        await using var entryStream = await entry.OpenAsync(
                            cancellationToken);

                        await storageFile.ReadTo(
                            output: PipeWriter.Create(
                                stream: entryStream,
                                writerOptions: new StreamPipeWriterOptions(
                                    leaveOpen: false)),
                            cancellationToken: cancellationToken);

                        processedFiles++;
                        totalBytes += file.SizeInBytes;

                        if (Log.IsEnabled(LogEventLevel.Debug))
                        {
                            var fileDuration = DateTime.UtcNow - fileStartTime;

                            Log.Debug(
                                "Successfully added file to archive: {FileName} ({FileSize:N0} bytes) in {DurationMs}ms. Speed: {Speed}",
                                entryName,
                                file.SizeInBytes,
                                fileDuration.TotalMilliseconds,
                                TransferSpeed.Format(file.SizeInBytes, fileDuration));
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Log.Warning(
                            "Bulk download operation cancelled while processing file {FileName}",
                            file.FullName);

                        throw;
                    }
                    catch (Exception e)
                    {
                        failedFiles++;

                        Log.Warning(
                            e,
                            "Failed to process file {FileName} ({FileExternalId}) from {BucketName}/{S3FileKey}",
                            file.FullName,
                            file.ExternalId,
                            bucketName,
                            s3FileKey.Value);
                    }
                }

                var totalDuration = DateTime.UtcNow - startTime;

                Log.Information(
                    "Completed bulk download operation. Processed: {ProcessedFiles}/{TotalFiles} files, " +
                    "Failed: {FailedFiles}, Total size: {TotalSize:N2} MB, " +
                    "Duration: {DurationSec:N1}s, Average speed: {Speed:N2} MB/s",
                    processedFiles,
                    totalFiles,
                    failedFiles,
                    totalBytes / 1024.0 / 1024.0,
                    totalDuration.TotalSeconds,
                    TransferSpeed.Format(totalBytes, totalDuration));
            }
            catch (Exception e)
            {
                Log.Error(
                    e,
                    "Bulk download operation failed after processing {ProcessedFiles}/{TotalFiles} files. Error: {ErrorMessage}",
                    processedFiles,
                    totalFiles,
                    e.Message);

                throw;
            }
        }
    }
}