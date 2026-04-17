using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Files.PreSignedLinks.RangeRequests;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.FileReading;
using Serilog;
using System.IO.Pipelines;
using PlikShare.Storages.HardDrive.Download;
using Serilog.Context;

namespace PlikShare.Storages.S3.Download;

public class S3DownloadOperation
{
    private class S3File(
        S3FileKey s3FileKey,
        FileEncryptionMetadata? fileEncryptionMetadata,
        long fileSizeInBytes,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        string bucketName,
        S3StorageClient s3StorageClient,
        Stream s3FileStream) : IFile
    {
        public async ValueTask WriteTo(
            PipeWriter output,
            CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;

            using (LogContext.PushProperty("SourceContext", typeof(HardDriveDownloadOperation).FullName))
            using (LogContext.PushProperty("FileExternalId", s3FileKey.FileExternalId))
            {
                Logger.Debug(
                    "Starting download operation for file {FileExternalId} from bucket {BucketName} with encryption {EncryptionType}",
                    s3FileKey.FileExternalId,
                    bucketName,
                    s3StorageClient.Encryption.Type);

                try
                {
                    Logger.Debug(
                        "Requesting file stream from S3 for {FileExternalId} at {S3Key}",
                        s3FileKey.FileExternalId,
                        s3FileKey.Value);

                    await s3StorageClient.WriteFileTo(
                        stream: s3FileStream,
                        output: output,
                        fileSizeInBytes: fileSizeInBytes,
                        encryptionMetadata: fileEncryptionMetadata,
                        workspaceEncryptionSession: workspaceEncryptionSession,
                        cancellationToken: cancellationToken);

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
        FileEncryptionMetadata? fileEncryptionMetadata,
        long fileSizeInBytes,
        BytesRange range,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        string bucketName,
        S3StorageClient s3StorageClient,
        Stream s3FileStream) : IFile
    {
        public async ValueTask WriteTo(
            PipeWriter output,
            CancellationToken cancellationToken)
        {
            Logger.Debug(
                "Starting download operation for file {FileExternalId} from bucket {BucketName} with encryption {EncryptionType}",
                s3FileKey.FileExternalId,
                bucketName,
                s3StorageClient.Encryption.Type);

            try
            {
                Logger.Debug(
                    "Requesting file stream from S3 for {FileExternalId} at {S3Key}",
                    s3FileKey.FileExternalId,
                    s3FileKey.Value);

                var startTime = DateTime.UtcNow;

                if (fileEncryptionMetadata is null)
                {
                    Logger.Debug(
                        "Starting unencrypted file transfer for {FileExternalId}",
                        s3FileKey.FileExternalId);

                    await s3FileStream.CopyToAsync(
                        destination: output,
                        cancellationToken: cancellationToken);
                }
                else if (fileEncryptionMetadata.FormatVersion == 1)
                {
                    Logger.Debug(
                        "Starting encrypted file transfer for {FileExternalId} using AES-256-GCM",
                        s3FileKey.FileExternalId);

                    if (s3StorageClient.Encryption is not ManagedStorageEncryption managedStorageEncryption)
                        throw new InvalidOperationException(
                            $"Storage encryption is supposed to be {nameof(ManagedStorageEncryption)} " +
                            $"but found {s3StorageClient.Encryption.GetType()}");

                    var ikm = managedStorageEncryption.GetEncryptionKey(
                        fileEncryptionMetadata.KeyVersion);

                    var encryptedRange = Aes256GcmStreamingV1.EncryptedBytesRangeCalculator.FromUnencryptedRange(
                        unencryptedRange: range,
                        unencryptedFileSize: fileSizeInBytes);
                    
                    await Aes256GcmStreamingV1.DecryptRange(
                        fileAesInputs: fileEncryptionMetadata.ToAesInputsV1(ikm),
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
                else if (fileEncryptionMetadata.FormatVersion == 2)
                {
                    Logger.Debug(
                        "Starting encrypted file transfer for {FileExternalId} using AES-256-GCM",
                        s3FileKey.FileExternalId);

                    var encryptedRange = Aes256GcmStreamingV2.EncryptedBytesRangeCalculator.FromUnencryptedRange(
                        unencryptedRange: range,
                        unencryptedFileSize: fileSizeInBytes,
                        chainStepsCount: fileEncryptionMetadata.ChainStepSalts.Count);

                    var ikm = workspaceEncryptionSession!.GetDekForVersion(
                        fileEncryptionMetadata.KeyVersion);

                    await Aes256GcmStreamingV2.DecryptRange(
                        fileAesInputs: fileEncryptionMetadata.ToAesInputsV2(ikm),
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
                    throw new InvalidOperationException(
                        $"Unsupported file encryption format version '{fileEncryptionMetadata.FormatVersion}' " +
                        $"for file '{s3FileKey.FileExternalId}'.");
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
        FileEncryptionMetadata? fileEncryptionMetadata,
        long fileSizeInBytes,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
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
            fileEncryptionMetadata,
            fileSizeInBytes,
            workspaceEncryptionSession,
            bucketName,
            s3StorageClient,
            stream);
    }

    public static async Task<IFile> GetFileRange(
        S3FileKey s3FileKey,
        FileEncryptionMetadata? fileEncryptionMetadata,
        long fileSizeInBytes,
        BytesRange range,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        string bucketName,
        S3StorageClient s3StorageClient,
        CancellationToken cancellationToken)
    {
        Logger.Debug(
            "Requesting ranged ({Range}) file stream from S3 for {FileExternalId} at {S3Key}",
            range,
            s3FileKey.FileExternalId,
            s3FileKey.Value);

        var fileBytesRange = fileEncryptionMetadata.CalculateFileRange(
            fileSizeInBytes: fileSizeInBytes,
            range: range);

        var stream = await s3StorageClient.GetFileRange(
            bucketName: bucketName,
            key: s3FileKey,
            range: fileBytesRange,
            cancellationToken: cancellationToken);
        
        return new S3FileRange(
            s3FileKey,
            fileEncryptionMetadata,
            fileSizeInBytes,
            range,
            workspaceEncryptionSession,
            bucketName,
            s3StorageClient,
            stream);
    }
}
