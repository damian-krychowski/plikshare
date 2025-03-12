using System.IO.Compression;
using System.IO.Pipelines;
using PlikShare.BulkDownload;
using PlikShare.Core.Clock;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.HardDrive.StorageClient;
using Serilog;
using Serilog.Events;

namespace PlikShare.Storages.HardDrive.BulkDownload;

public class HardDriveBulkDownloadOperation(IClock clock)
{
    private readonly Serilog.ILogger _logger = Log.ForContext<HardDriveBulkDownloadOperation>();

    public async Task Execute(
        BulkDownloadDetails bulkDownloadDetails,
        string bucketName,
        HardDriveStorageClient hardDriveStorage,
        PipeWriter responsePipeWriter,
        CancellationToken cancellationToken)
    {
        var startTime = clock.UtcNow;
        var totalFiles = bulkDownloadDetails.Files.Count;
        var processedFiles = 0;
        var failedFiles = 0;
        var missingFiles = 0;
        var totalBytes = 0L;

        _logger.Information(
            "Starting bulk download operation for {FileCount} files from {StoragePath}, bucket: {BucketName}, encryption: {EncryptionType}",
            totalFiles,
            hardDriveStorage.Details.FullPath,
            bucketName,
            hardDriveStorage.EncryptionType);

        var (files, folderSubtree) = bulkDownloadDetails;

        var uniqueFileNames = new UniqueFileNames(
            capacity: totalFiles);

        try
        {
            using var archive = new ZipArchive(
                stream: responsePipeWriter.AsStream(),
                mode: ZipArchiveMode.Create,
                leaveOpen: true);
            
            foreach (var file in files.OrderBy(f => folderSubtree.GetLevelInTree(f.FolderId)))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileStartTime = clock.UtcNow;
                var filePath = Path.Combine(
                    hardDriveStorage.Details.FullPath,
                    bucketName,
                    file.ExternalId.Value);

                if (!File.Exists(filePath))
                {
                    missingFiles++;

                    _logger.Warning(
                        "File not found: {FileName} (ExternalId: {FileExternalId}) at path: {FilePath}",
                        file.FullName,
                        file.ExternalId,
                        filePath);

                    continue;
                }

                var folderPath = folderSubtree.GetPath(
                    folderId: file.FolderId);

                try
                {
                    _logger.Debug(
                        "Processing file {FileNumber}/{TotalFiles}: {FileName} at path {FilePath}",
                        processedFiles + 1,
                        totalFiles,
                        file.FullName,
                        folderPath);

                    var (entryName, wasFileNameCollisionDetected) = uniqueFileNames.EnsureUniqueFileName(
                        fullFileName: file.FullName,
                        folderPath: folderPath);

                    if (wasFileNameCollisionDetected)
                    {
                        _logger.Debug(
                            "File name collision detected. Original: {FilePath}{FileName}, Generated unique name: {UniqueName}",
                            folderPath, file.FullName, entryName);
                    }

                    var entry = archive.CreateEntry(
                        entryName,
                        CompressionLevel.NoCompression);

                    var fileInfo = new FileInfo(filePath);

                    _logger.Debug(
                        "Reading file {FileName} ({FileSize:N0} bytes, LastModified: {LastModified})",
                        file.FullName,
                        fileInfo.Length,
                        fileInfo.LastWriteTimeUtc);

                    await using var entryStream = entry.Open();
                    await using var fileStream = new FileStream(
                        filePath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: PlikShareStreams.DefaultBufferSize,
                        useAsync: true);

                    if (hardDriveStorage.EncryptionType == StorageEncryptionType.None)
                    {
                        _logger.Debug(
                            "Starting unencrypted file transfer for {FileName}",
                            file.FullName);

                        await fileStream.CopyToAsync(
                            entryStream,
                            PlikShareStreams.DefaultBufferSize,
                            cancellationToken);
                    }
                    else if (hardDriveStorage.EncryptionType == StorageEncryptionType.Managed)
                    {
                        _logger.Debug(
                            "Starting encrypted file transfer for {FileName} using AES-256-GCM",
                            file.FullName);

                        await Aes256GcmStreaming.Decrypt(
                            keyProvider: hardDriveStorage.EncryptionKeyProvider!,
                            fileSizeInBytes: file.SizeInBytes,
                            input: PipeReader.Create(
                                stream: fileStream,
                                readerOptions: new StreamPipeReaderOptions(
                                    bufferSize: PlikShareStreams.DefaultBufferSize,
                                    leaveOpen: false)),
                            output: PipeWriter.Create(
                                stream: entryStream,
                                writerOptions: new StreamPipeWriterOptions(
                                    leaveOpen: false)),
                            cancellationToken);
                    }
                    else
                    {
                        throw new NotImplementedException(
                            $"Encryption type '{hardDriveStorage.EncryptionType}' is not implemented for Storage#{hardDriveStorage.StorageId}");
                    }

                    processedFiles++;
                    totalBytes += file.SizeInBytes;

                    if (_logger.IsEnabled(LogEventLevel.Debug))
                    {
                        var fileDuration = clock.UtcNow - fileStartTime;

                        _logger.Debug(
                            "Successfully added file to archive: {FileName} ({FileSize:N0} bytes) in {DurationMs}ms. Speed: {Speed}",
                            entryName,
                            file.SizeInBytes,
                            fileDuration.TotalMilliseconds,
                            TransferSpeed.Format(file.SizeInBytes, fileDuration));
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.Warning(
                        "Bulk download operation cancelled while processing file {FileName}",
                        file.FullName);

                    throw;
                }
                catch (UnauthorizedAccessException e)
                {
                    failedFiles++;

                    _logger.Error(
                        e,
                        "Access denied while processing file {FileName} at {FilePath}",
                        file.FullName,
                        filePath);
                }
                catch (IOException e)
                {
                    failedFiles++;

                    _logger.Error(
                        e,
                        "IO error while processing file {FileName} at {FilePath}",
                        file.FullName,
                        filePath);
                }
                catch (Exception e)
                {
                    failedFiles++;

                    _logger.Warning(
                        e,
                        "Error processing file {FileName} ({FileExternalId}) at {FilePath}",
                        file.FullName,
                        file.ExternalId,
                        filePath);
                }
            }

            var totalDuration = clock.UtcNow - startTime;

            _logger.Information(
                "Completed bulk download operation. Processed: {ProcessedFiles}/{TotalFiles} files, " +
                "Failed: {FailedFiles}, Missing: {MissingFiles}, Total size: {TotalSize:N2} MB, " +
                "Duration: {DurationSec:N1}s, Average speed: {Speed:N2} MB/s",
                processedFiles,
                totalFiles,
                failedFiles,
                missingFiles,
                totalBytes / 1024.0 / 1024.0,
                totalDuration.TotalSeconds,
                TransferSpeed.Format(totalBytes, totalDuration));
        }
        catch (Exception e)
        {
            _logger.Error(
                e,
                "Bulk download operation failed after processing {ProcessedFiles}/{TotalFiles} files. Error: {ErrorMessage}",
                processedFiles,
                totalFiles,
                e.Message);

            throw;
        }
    }
}