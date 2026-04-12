using System.IO.Pipelines;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Files.PreSignedLinks.RangeRequests;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.FileReading;
using Serilog;

namespace PlikShare.Storages.S3.Download;

public class S3DownloadOperation
{
    private class S3File(
        S3FileKey s3FileKey,
        long fileSizeInBytes,
        string bucketName,
        S3StorageClient s3StorageClient,
        Stream s3FileStream) : IFile
    {
        public async Task WriteTo(
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

                    var keyProvider = s3StorageClient
                        .GetManagedEncryptionKeyProviderOrThrow();

                    await Aes256GcmStreaming.Decrypt(
                        getEncryptionKeyFunc: version => keyProvider.GetEncryptionKey(
                            version),
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

        public void Dispose()
        {
            s3FileStream.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            await s3FileStream.DisposeAsync();
        }
    }

    private class S3FileRange(
        S3FileKey s3FileKey,
        long fileSizeInBytes,
        FileEncryption fileEncryption,
        BytesRange range,
        string bucketName,
        S3StorageClient s3StorageClient,
        Stream s3FileStream) : IFile
    {
        public async Task WriteTo(
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
                    Logger.Debug(
                        "Starting unencrypted file transfer for {FileExternalId}",
                        s3FileKey.FileExternalId);

                    await s3FileStream.CopyToAsync(
                        destination: output,
                        cancellationToken: cancellationToken);
                }
                else if (s3StorageClient.EncryptionType == StorageEncryptionType.Managed)
                {
                    var encryptedRange = Aes256GcmStreaming.EncryptedBytesRangeCalculator.FromUnencryptedRange(
                        unencryptedRange: range,
                        unencryptedFileSize: fileSizeInBytes);
                    
                    Logger.Debug(
                        "Starting encrypted file transfer for {FileExternalId} using AES-256-GCM",
                    s3FileKey.FileExternalId);
                    
                    var keyProvider = s3StorageClient
                        .GetManagedEncryptionKeyProviderOrThrow();

                    await Aes256GcmStreaming.DecryptRange(
                        getEncryptionKeyFunc: version => keyProvider.GetEncryptionKey(version),
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

        public void Dispose()
        {
            s3FileStream.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            await s3FileStream.DisposeAsync();
        }
    }

    private static readonly Serilog.ILogger Logger = Log.ForContext<S3DownloadOperation>();

    public static async Task<IFile> GetFile(
        S3FileKey s3FileKey,
        long fileSizeInBytes,
        string bucketName,
        S3StorageClient s3StorageClient,
        CancellationToken cancellationToken)
    {
        Logger.Debug(
            "Requesting file stream from S3 for {FileExternalId} at {S3Key}",
            s3FileKey.FileExternalId,
            s3FileKey.Value);

        var stream = await s3StorageClient.GetFile(
            bucketName: bucketName,
            key: s3FileKey,
            cancellationToken: cancellationToken);

        return new S3File(
            s3FileKey,
            fileSizeInBytes, 
            bucketName, 
            s3StorageClient, 
            stream);
    }

    public static async Task<IFile> GetFileRange(
        S3FileKey s3FileKey,
        FileEncryption fileEncryption,
        long fileSizeInBytes,
        BytesRange range,
        string bucketName,
        S3StorageClient s3StorageClient,
        CancellationToken cancellationToken)
    {
        Logger.Debug(
            "Requesting ranged ({Range}) file stream from S3 for {FileExternalId} at {S3Key}",
            range,
            s3FileKey.FileExternalId,
            s3FileKey.Value);

        if (s3StorageClient.EncryptionType == StorageEncryptionType.None)
        {
            var stream = await s3StorageClient.GetFileRange(
                bucketName: bucketName,
                key: s3FileKey,
                range: range,
                cancellationToken: cancellationToken);

            return new S3FileRange(
                s3FileKey,
                fileSizeInBytes,
                fileEncryption,
                range,
                bucketName,
                s3StorageClient,
                stream);
        }

        if (s3StorageClient.EncryptionType == StorageEncryptionType.Managed)
        {
            var encryptedRange = Aes256GcmStreaming.EncryptedBytesRangeCalculator.FromUnencryptedRange(
                unencryptedRange: range,
                unencryptedFileSize: fileSizeInBytes);

            var stream = await s3StorageClient.GetFileRange(
                bucketName: bucketName,
                key: s3FileKey,
                range: new BytesRange(
                    Start: encryptedRange.FirstSegment.Start,
                    End: encryptedRange.LastSegment.End),
                cancellationToken: cancellationToken);

            return new S3FileRange(
                s3FileKey,
                fileSizeInBytes,
                fileEncryption,
                range,
                bucketName,
                s3StorageClient,
                stream);
        }

        throw new NotImplementedException(
            $"Encryption type '{s3StorageClient.EncryptionType}' is not implemented for Storage#{s3StorageClient.StorageId}");
    }
}