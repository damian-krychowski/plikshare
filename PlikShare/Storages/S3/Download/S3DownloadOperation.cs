using System.IO.Pipelines;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Files.PreSignedLinks.RangeRequests;
using PlikShare.Storages.Encryption;
using Serilog;

namespace PlikShare.Storages.S3.Download;

public class S3DownloadOperation
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<S3DownloadOperation>();
    
    /// <exception cref="OperationCanceledException"></exception>
    public static async Task ExecuteForFullFile(
        S3FileKey s3FileKey,
        long fileSizeInBytes,
        string bucketName,
        S3StorageClient s3StorageClient,
        PipeWriter output,
        CancellationToken cancellationToken)
    {

        Logger.Debug(
            "Starting download operation for file {FileExternalId} from bucket {BucketName} with encryption {EncryptionType}",
            s3FileKey.FileExternalId,
            bucketName,
            s3StorageClient.EncryptionType);

        try
        {
            Logger.Debug(
                "Requesting file stream from S3 for {FileExternalId} at {S3Key}",
                s3FileKey.FileExternalId,
                s3FileKey.Value);

            await using var s3FileStream = await s3StorageClient.GetFile(
                bucketName: bucketName,
                key: s3FileKey,
                cancellationToken: cancellationToken);

            var startTime = DateTime.UtcNow;

            if (s3StorageClient.EncryptionType == StorageEncryptionType.None)
            {
                Logger.Debug(
                    "Starting unencrypted file transfer for {FileExternalId}",
                    s3FileKey.FileExternalId);

                await s3FileStream.CopyToAsync(
                    destination: output,
                    cancellationToken: cancellationToken);
            }
            else if (s3StorageClient.EncryptionType == StorageEncryptionType.Managed)
            {
                Logger.Debug(
                    "Starting encrypted file transfer for {FileExternalId} using AES-256-GCM",
                    s3FileKey.FileExternalId);

                await Aes256GcmStreaming.Decrypt(
                    keyProvider: s3StorageClient.EncryptionKeyProvider!,
                    fileSizeInBytes: fileSizeInBytes,
                    input: PipeReader.Create(
                        s3FileStream,
                        new StreamPipeReaderOptions(
                            bufferSize: PlikShareStreams.DefaultBufferSize,
                            leaveOpen: false)),
                    output: output,
                    cancellationToken);
            }
            else
            {
                throw new NotImplementedException(
                    $"Encryption type '{s3StorageClient.EncryptionType}' is not implemented for Storage#{s3StorageClient.StorageId}");
            }

            var duration = DateTime.UtcNow - startTime;

            Logger.Debug(
                "Successfully completed download operation for {FileExternalId} in {DurationMs}ms",
                s3FileKey.FileExternalId,
                duration.TotalMilliseconds);
        }
        catch (OperationCanceledException)
        {
            Logger.Warning(
                "Download operation cancelled for file {FileExternalId} from {BucketName}/{S3Key}",
                s3FileKey.FileExternalId,
                bucketName,
                s3FileKey.Value);

            throw;
        }
        catch (Exception e)
        {
            Logger.Error(
                e,
                "Failed to download file {FileExternalId} from {BucketName}/{S3Key}. Error: {ErrorMessage}",
                s3FileKey.FileExternalId,
                bucketName,
                s3FileKey.Value,
                e.Message);

            throw;
        }
    }

    /// <exception cref="OperationCanceledException"></exception>
    public static async Task ExecuteForRange(
        S3FileKey s3FileKey,
        FileEncryption fileEncryption,
        long fileSizeInBytes,
        BytesRange range,
        string bucketName,
        S3StorageClient s3StorageClient,
        PipeWriter output,
        CancellationToken cancellationToken)
    {
        Logger.Debug(
            "Starting download operation for file {FileExternalId} from bucket {BucketName} with encryption {EncryptionType}",
            s3FileKey.FileExternalId,
            bucketName,
            s3StorageClient.EncryptionType);

        try
        {
            Logger.Debug(
                "Requesting file stream from S3 for {FileExternalId} at {S3Key}",
                s3FileKey.FileExternalId,
                s3FileKey.Value);

            var startTime = DateTime.UtcNow;

            if (s3StorageClient.EncryptionType == StorageEncryptionType.None)
            {
                await using var s3FileStream = await s3StorageClient.GetFileRange(
                    bucketName: bucketName,
                    key: s3FileKey,
                    range: range,
                    cancellationToken: cancellationToken);

                Logger.Debug(
                    "Starting unencrypted file transfer for {FileExternalId}",
                    s3FileKey.FileExternalId);

                await s3FileStream.CopyToAsync(
                    destination: output,
                    cancellationToken: cancellationToken);

                await output.CompleteAsync();
            }
            else if (s3StorageClient.EncryptionType == StorageEncryptionType.Managed)
            {
                var encryptedRange = Aes256GcmStreaming.EncryptedBytesRangeCalculator.FromUnencryptedRange(
                    unencryptedRange: range,
                    unencryptedFileSize: fileSizeInBytes);

                await using var s3FileStream = await s3StorageClient.GetFileRange(
                    bucketName: bucketName,
                    key: s3FileKey,
                    range: new BytesRange(
                        Start: encryptedRange.FirstSegment.Start,
                        End: encryptedRange.LastSegment.End),
                    cancellationToken: cancellationToken);

                Logger.Debug(
                    "Starting encrypted file transfer for {FileExternalId} using AES-256-GCM",
                s3FileKey.FileExternalId);

                await Aes256GcmStreaming.DecryptRange(
                    keyProvider: s3StorageClient.EncryptionKeyProvider!,
                    encryptionMetadata: fileEncryption.Metadata!,
                    range: encryptedRange,
                    fileSizeInBytes: fileSizeInBytes,
                    input: PipeReader.Create(
                        s3FileStream,
                        new StreamPipeReaderOptions(
                            bufferSize: PlikShareStreams.DefaultBufferSize,
                            leaveOpen: false)),
                    output: output,
                    cancellationToken);
            }
            else
            {
                throw new NotImplementedException(
                    $"Encryption type '{s3StorageClient.EncryptionType}' is not implemented for Storage#{s3StorageClient.StorageId}");
            }

            var duration = DateTime.UtcNow - startTime;

            Logger.Debug(
                "Successfully completed download operation for {FileExternalId} in {DurationMs}ms",
                s3FileKey.FileExternalId,
                duration.TotalMilliseconds);
        }
        catch (OperationCanceledException)
        {
            Logger.Warning(
                "Download operation cancelled for file {FileExternalId} from {BucketName}/{S3Key}",
                s3FileKey.FileExternalId,
                bucketName,
                s3FileKey.Value);

            throw;
        }
        catch (Exception e)
        {
            Logger.Error(
                e,
                "Failed to download file {FileExternalId} from {BucketName}/{S3Key}. Error: {ErrorMessage}",
                s3FileKey.FileExternalId,
                bucketName,
                s3FileKey.Value,
                e.Message);

            throw;
        }
    }
}