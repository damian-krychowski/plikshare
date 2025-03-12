using System.IO.Pipelines;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Files.PreSignedLinks.RangeRequests;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Exceptions;
using PlikShare.Storages.HardDrive.StorageClient;
using Serilog;

namespace PlikShare.Storages.HardDrive.Download;


public class HardDriveDownloadOperation
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<HardDriveDownloadOperation>();


    /// <exception cref="FileNotFoundInStorageException"></exception>
    /// <exception cref="OperationCanceledException"></exception>
    public static async Task ExecuteForFullFile(
        S3FileKey s3FileKey,
        long fileSizeInBytes,
        string bucketName,
        HardDriveStorageClient hardDriveStorageClient,
        PipeWriter output,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        var filePath = Path.Combine(
            hardDriveStorageClient.Details.FullPath,
            bucketName,
            s3FileKey.FileExternalId.Value);

        Logger.Debug(
            "Starting download operation for file {FileExternalId} from {StoragePath}, bucket: {BucketName}, encryption: {EncryptionType}",
            s3FileKey.FileExternalId,
            hardDriveStorageClient.Details.FullPath,
            bucketName,
            hardDriveStorageClient.EncryptionType);

        if (!File.Exists(filePath))
        {
            Logger.Warning(
                "File not found: {FileExternalId} at path: {FilePath}",
                s3FileKey.FileExternalId,
                filePath);

            throw new FileNotFoundInStorageException(
                $"File '{s3FileKey.FileExternalId}' was not found in Storage '{hardDriveStorageClient.ExternalId}'");
        }

        try
        {
            Logger.Debug(
                "Reading file {FileExternalId} ({FileSize:N0} bytes)",
                s3FileKey.FileExternalId,
                fileSizeInBytes);

            await using var stream = new FileStream(
                path: filePath,
                mode: FileMode.Open,
                access: FileAccess.Read,
                share: FileShare.Read,
                bufferSize: PlikShareStreams.DefaultBufferSize,
                useAsync: true);

            if (hardDriveStorageClient.EncryptionType == StorageEncryptionType.None)
            {
                Logger.Debug(
                    "Starting unencrypted file transfer for {FileExternalId}",
                    s3FileKey.FileExternalId);
                
                await stream.CopyToAsync(
                    destination: output,
                    cancellationToken: cancellationToken);

                var streamDuration = DateTime.UtcNow - startTime;
                var streamSpeed = fileSizeInBytes / Math.Max(1, streamDuration.TotalSeconds);

                Logger.Debug(
                    "Completed unencrypted file transfer: {FileSize:N0} bytes in {DurationMs}ms. Speed: {Speed:N2} MB/s",
                    fileSizeInBytes,
                    streamDuration.TotalMilliseconds,
                    streamSpeed / 1024.0 / 1024.0);
            }
            else if (hardDriveStorageClient.EncryptionType == StorageEncryptionType.Managed)
            {
                Logger.Debug(
                    "Starting encrypted file transfer for {FileExternalId} using AES-256-GCM",
                    s3FileKey.FileExternalId);
                
                await Aes256GcmStreaming.Decrypt(
                    keyProvider: hardDriveStorageClient.EncryptionKeyProvider!,
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

                Logger.Debug(
                    "Completed encrypted file transfer: {FileSize:N0} bytes in {DurationMs}ms. Speed: {Speed:N2} MB/s",
                    fileSizeInBytes,
                    decryptDuration.TotalMilliseconds,
                    decryptSpeed / 1024.0 / 1024.0);
            }
            else
            {
                Logger.Error(
                    "Unsupported encryption type {EncryptionType} for Storage {StorageId}",
                    hardDriveStorageClient.EncryptionType,
                    hardDriveStorageClient.StorageId);

                throw new NotImplementedException(
                    $"Encryption type '{hardDriveStorageClient.EncryptionType}' is not implemented for Storage#{hardDriveStorageClient.StorageId}");
            }

            var totalDuration = DateTime.UtcNow - startTime;
            var averageSpeed = fileSizeInBytes / Math.Max(1, totalDuration.TotalSeconds);

            Logger.Information(
                "Successfully completed download operation for {FileExternalId}. Size: {FileSize:N2} MB, " +
                "Duration: {DurationSec:N1}s, Average speed: {Speed:N2} MB/s",
                s3FileKey.FileExternalId,
                fileSizeInBytes / 1024.0 / 1024.0,
                totalDuration.TotalSeconds,
                averageSpeed / 1024.0 / 1024.0);
        }
        catch (UnauthorizedAccessException e)
        {
            Logger.Error(
                e,
                "Access denied while downloading file {FileExternalId} from {FilePath}",
                s3FileKey.FileExternalId,
                filePath);

            throw;
        }
        catch (IOException e)
        {
            Logger.Error(
                e,
                "IO error while downloading file {FileExternalId} from {FilePath}",
                s3FileKey.FileExternalId,
                filePath);

            throw;
        }
        catch (OperationCanceledException)
        {
            Logger.Warning(
                "Download operation cancelled for file {FileExternalId}",
                s3FileKey.FileExternalId);

            throw;
        }
        catch (Exception e)
        {
            Logger.Error(
                e,
                "Failed to download file {FileExternalId} from {FilePath}. Error: {ErrorMessage}",
                s3FileKey.FileExternalId,
                filePath,
                e.Message);

            throw;
        }
    }

    /// <exception cref="FileNotFoundInStorageException"></exception>
    /// <exception cref="OperationCanceledException"></exception>
    public static async Task ExecuteForRange(
        S3FileKey s3FileKey,
        FileEncryption fileEncryption,
        long fileSizeInBytes,
        BytesRange range,
        string bucketName,
        HardDriveStorageClient hardDriveStorageClient,
        PipeWriter output,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        var filePath = Path.Combine(
            hardDriveStorageClient.Details.FullPath,
            bucketName,
            s3FileKey.FileExternalId.Value);

        Logger.Debug(
            "Starting download operation for file {FileExternalId} from {StoragePath}, bucket: {BucketName}, encryption: {EncryptionType}",
            s3FileKey.FileExternalId,
            hardDriveStorageClient.Details.FullPath,
            bucketName,
            hardDriveStorageClient.EncryptionType);

        if (!File.Exists(filePath))
        {
            Logger.Warning(
                "File not found: {FileExternalId} at path: {FilePath}",
                s3FileKey.FileExternalId,
                filePath);
            
            throw new FileNotFoundInStorageException(
                $"File '{s3FileKey.FileExternalId}' was not found in Storage '{hardDriveStorageClient.ExternalId}'");
        }

        try
        {
            Logger.Debug(
                "Reading file {FileExternalId} ({FileSize:N0} bytes)",
                s3FileKey.FileExternalId,
                fileSizeInBytes);
            
            if (hardDriveStorageClient.EncryptionType == StorageEncryptionType.None)
            {
                Logger.Debug(
                    "Starting unencrypted file transfer for {FileExternalId}",
                    s3FileKey.FileExternalId);
                
                await ReadFileRange(
                    filePath,
                    range, 
                    output, 
                    cancellationToken);

                var streamDuration = DateTime.UtcNow - startTime;
                var streamSpeed = fileSizeInBytes / Math.Max(1, streamDuration.TotalSeconds);

                Logger.Debug(
                    "Completed unencrypted file transfer: {FileSize:N0} bytes in {DurationMs}ms. Speed: {Speed:N2} MB/s",
                    fileSizeInBytes,
                    streamDuration.TotalMilliseconds,
                    streamSpeed / 1024.0 / 1024.0);
            }
            else if (hardDriveStorageClient.EncryptionType == StorageEncryptionType.Managed)
            {
                Logger.Debug(
                    "Starting encrypted file transfer for {FileExternalId} using AES-256-GCM",
                    s3FileKey.FileExternalId);

                await ReadEncryptedFileRange(
                    filePath,
                    fileEncryption,
                    fileSizeInBytes,
                    range, 
                    hardDriveStorageClient, 
                    output, 
                    cancellationToken);

                var decryptDuration = DateTime.UtcNow - startTime;
                var decryptSpeed = fileSizeInBytes / Math.Max(1, decryptDuration.TotalSeconds);

                Logger.Debug(
                    "Completed encrypted file transfer: {FileSize:N0} bytes in {DurationMs}ms. Speed: {Speed:N2} MB/s",
                    fileSizeInBytes,
                    decryptDuration.TotalMilliseconds,
                    decryptSpeed / 1024.0 / 1024.0);
            }
            else
            {
                Logger.Error(
                    "Unsupported encryption type {EncryptionType} for Storage {StorageId}",
                    hardDriveStorageClient.EncryptionType,
                    hardDriveStorageClient.StorageId);

                throw new NotImplementedException(
                    $"Encryption type '{hardDriveStorageClient.EncryptionType}' is not implemented for Storage#{hardDriveStorageClient.StorageId}");
            }

            var totalDuration = DateTime.UtcNow - startTime;
            var averageSpeed = fileSizeInBytes / Math.Max(1, totalDuration.TotalSeconds);

            Logger.Information(
                "Successfully completed download operation for {FileExternalId}. Size: {FileSize:N2} MB, " +
                "Duration: {DurationSec:N1}s, Average speed: {Speed:N2} MB/s",
                s3FileKey.FileExternalId,
                fileSizeInBytes / 1024.0 / 1024.0,
                totalDuration.TotalSeconds,
                averageSpeed / 1024.0 / 1024.0);
        }
        catch (UnauthorizedAccessException e)
        {
            Logger.Error(
                e,
                "Access denied while downloading file {FileExternalId} from {FilePath}",
                s3FileKey.FileExternalId,
                filePath);

            throw;
        }
        catch (IOException e)
        {
            Logger.Error(
                e,
                "IO error while downloading file {FileExternalId} from {FilePath}",
                s3FileKey.FileExternalId,
                filePath);

            throw;
        }
        catch (OperationCanceledException)
        {
            Logger.Warning(
                "Download operation cancelled for file {FileExternalId}",
                s3FileKey.FileExternalId);

            //we don't throw here as constant cancelling is normal behavior for video players
            //throw;

            throw;
        }
        catch (Exception e)
        {
            Logger.Error(
                e,
                "Failed to download file {FileExternalId} from {FilePath}. Error: {ErrorMessage}",
                s3FileKey.FileExternalId,
                filePath,
                e.Message);

            throw;
        }
    }

    private static async Task ReadFileRange(
        string filePath,
        BytesRange range,
        PipeWriter output, 
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path: filePath,
            mode: FileMode.Open,
            access: FileAccess.Read,
            share: FileShare.Read,
            bufferSize: PlikShareStreams.DefaultBufferSize,
            useAsync: true);

        stream.Seek(range.Start, SeekOrigin.Begin);
        
        await FileRangeProcessing.CopyBytes(
            input: PipeReader.Create(
                stream,
                new StreamPipeReaderOptions(
                    bufferSize: PlikShareStreams.DefaultBufferSize,
                    leaveOpen: false)),
            lengthToCopy: range.Length,
            output: output,
            cancellationToken: cancellationToken);
    }

    private static async Task ReadEncryptedFileRange(
        string filePath,
        FileEncryption fileEncryption,
        long fileSizeInBytes,
        BytesRange range,
        HardDriveStorageClient hardDriveStorageClient,
        PipeWriter output,
        CancellationToken cancellationToken)
    {
        var encryptedRange = Aes256GcmStreaming.EncryptedBytesRangeCalculator.FromUnencryptedRange(
            unencryptedRange: range,
            unencryptedFileSize: fileSizeInBytes);

        await using var stream = new FileStream(
            path: filePath,
            mode: FileMode.Open,
            access: FileAccess.Read,
            share: FileShare.Read,
            bufferSize: PlikShareStreams.DefaultBufferSize,
            useAsync: true);

        stream.Seek(encryptedRange.FirstSegment.Start, SeekOrigin.Begin);

        await Aes256GcmStreaming.DecryptRange(
            keyProvider: hardDriveStorageClient.EncryptionKeyProvider!,
            encryptionMetadata: fileEncryption.Metadata!,
            range: encryptedRange,
            fileSizeInBytes: fileSizeInBytes,
            input: PipeReader.Create(
                stream,
                new StreamPipeReaderOptions(
                    bufferSize: PlikShareStreams.DefaultBufferSize,
                    leaveOpen: false)),
            output: output,
            cancellationToken);
    }
}