using System.IO.Compression;
using System.IO.Pipelines;
using PlikShare.BulkDownload;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Storages.Encryption;
using Serilog;
using Serilog.Events;

namespace PlikShare.Storages.S3.BulkDownload;

public class S3BulkDownloadOperation
{
    private readonly Serilog.ILogger _logger = Log.ForContext<S3BulkDownloadOperation>();

    public async Task Execute(
        BulkDownloadDetails bulkDownloadDetails,
        string bucketName,
        S3StorageClient s3StorageClient,
        PipeWriter responsePipeWriter,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        var totalFiles = bulkDownloadDetails.Files.Count;
        var processedFiles = 0;
        var failedFiles = 0;
        var totalBytes = 0L;

        _logger.Information(
            "Starting bulk download operation for {FileCount} files from bucket {BucketName}",
            totalFiles,
            bucketName);

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
                    _logger.Debug(
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
                        _logger.Debug(
                            "File name collision detected. Original: {FilePath}{FileName}, Generated unique name: {UniqueName}",
                            folderPath,file.FullName, entryName);
                    }

                    var entry = archive.CreateEntry(
                        entryName,
                        CompressionLevel.NoCompression);

                    _logger.Debug(
                        "Downloading file {FileExternalId} from {BucketName}/{S3FileKey}",
                        file.ExternalId,
                        bucketName,
                        s3FileKey.Value);

                    await using var s3FileStream = await s3StorageClient.GetFile(
                        bucketName: bucketName,
                        key: s3FileKey,
                        cancellationToken: cancellationToken);

                    await using var entryStream = entry.Open();

                    if (s3StorageClient.EncryptionType == StorageEncryptionType.None)
                    {
                        _logger.Debug(
                            "Starting unencrypted file transfer for {FileName}",
                            file.FullName);

                        await s3FileStream.CopyToAsync(
                            entryStream,
                            PlikShareStreams.DefaultBufferSize,
                            cancellationToken);
                    }
                    else if (s3StorageClient.EncryptionType == StorageEncryptionType.Managed)
                    {
                        _logger.Debug(
                            "Starting encrypted file transfer for {FileName} using AES-256-GCM",
                            file.FullName);

                        await Aes256GcmStreaming.Decrypt(
                            keyProvider: s3StorageClient.EncryptionKeyProvider!,
                            fileSizeInBytes: file.SizeInBytes,
                            input: PipeReader.Create(
                                stream: s3FileStream,
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
                            $"Encryption type '{s3StorageClient.EncryptionType}' is not implemented for Storage#{s3StorageClient.StorageId}");
                    }

                    processedFiles++;
                    totalBytes += file.SizeInBytes;

                    if (_logger.IsEnabled(LogEventLevel.Debug))
                    {
                        var fileDuration = DateTime.UtcNow - fileStartTime;

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
                catch (Exception e)
                {
                    failedFiles++;

                    _logger.Warning(
                        e,
                        "Failed to process file {FileName} ({FileExternalId}) from {BucketName}/{S3FileKey}",
                        file.FullName,
                        file.ExternalId,
                        bucketName,
                        s3FileKey.Value);
                }
            }

            var totalDuration = DateTime.UtcNow - startTime;

            _logger.Information(
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