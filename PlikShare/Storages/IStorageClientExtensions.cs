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

                    var s3FileKey = new FileKey
                    {
                        FileExternalId = file.ExternalId,
                        KeySecretPart = file.KeySecretPart
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
                            "Downloading file {FileExternalId} from {BucketName}/{FileKey}",
                            file.ExternalId,
                            bucketName,
                            s3FileKey.Value);

                        await using var storageFile = await client.DownloadFile(
                            fileDetails: new DownloadFileDetails(
                                FileKey: s3FileKey,
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
                            "Failed to process file {FileName} ({FileExternalId}) from {BucketName}/{FileKey}",
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