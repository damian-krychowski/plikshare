using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Files.PreSignedLinks.RangeRequests;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Exceptions;
using PlikShare.Storages.FileReading;
using PlikShare.Storages.HardDrive.StorageClient;
using Serilog;
using System.IO.Pipelines;
using Serilog.Context;

namespace PlikShare.Storages.HardDrive.Download;

public class HardDriveDownloadOperation
{
    private class HdFile(
        S3FileKey s3FileKey,
        FileEncryptionMetadata? fileEncryptionMetadata,
        long fileSizeInBytes,
        string filePath,
        FullEncryptionSession? fullEncryptionSession,
        HardDriveStorageClient hardDriveStorageClient,
        FileStream stream) : IFile
    {
        public async ValueTask WriteTo(
            PipeWriter output, 
            CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;

            using (LogContext.PushProperty("SourceContext", typeof(HardDriveDownloadOperation).FullName))
            using (LogContext.PushProperty("FileExternalId", s3FileKey.FileExternalId))
            {
                try
                {
                    await hardDriveStorageClient.WriteFileTo(
                        stream: stream,
                        output: output,
                        fileSizeInBytes: fileSizeInBytes,
                        encryptionMetadata: fileEncryptionMetadata,
                        fullEncryptionSession: fullEncryptionSession,
                        cancellationToken: cancellationToken);

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
        }

        public void Dispose()
        {
            stream.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            await stream.DisposeAsync();
        }
    }

    private class HdFileRange(
        S3FileKey s3FileKey,
        long fileSizeInBytes,
        FileEncryptionMetadata? fileEncryptionMetadata,
        BytesRange range,
        string filePath,
        FullEncryptionSession? fullEncryptionSession,
        HardDriveStorageClient hardDriveStorageClient,
        Stream stream) : IFile
    {
        public async ValueTask WriteTo(
            PipeWriter output, 
            CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                Logger.Debug(
                    "Reading file {FileExternalId} ({FileSize:N0} bytes)",
                    s3FileKey.FileExternalId,
                    fileSizeInBytes);

                if (fileEncryptionMetadata is null)
                {
                    Logger.Debug(
                        "Starting unencrypted file transfer for {FileExternalId}",
                        s3FileKey.FileExternalId);

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


                    var streamDuration = DateTime.UtcNow - startTime;
                    var streamSpeed = fileSizeInBytes / Math.Max(1, streamDuration.TotalSeconds);

                    Logger.Debug(
                        "Completed unencrypted file transfer: {FileSize:N0} bytes in {DurationMs}ms. Speed: {Speed:N2} MB/s",
                        fileSizeInBytes,
                        streamDuration.TotalMilliseconds,
                        streamSpeed / 1024.0 / 1024.0);
                }
                else if (fileEncryptionMetadata.FormatVersion == 1)
                {
                    Logger.Debug(
                        "Starting encrypted file transfer for {FileExternalId} using AES-256-GCM",
                        s3FileKey.FileExternalId);
                    
                    var encryptedRange = Aes256GcmStreamingV1.EncryptedBytesRangeCalculator.FromUnencryptedRange(
                        unencryptedRange: range,
                        unencryptedFileSize: fileSizeInBytes);
                    
                    stream.Seek(encryptedRange.FirstSegment.Start, SeekOrigin.Begin);

                    var ikm = hardDriveStorageClient.GetEncryptionKey(
                        version: fileEncryptionMetadata.KeyVersion,
                        fullEncryptionSession: fullEncryptionSession);

                    await Aes256GcmStreamingV1.DecryptRange(
                        fileAesInputs:  fileEncryptionMetadata.ToAesInputsV1(ikm),
                        range: encryptedRange,
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
                else if (fileEncryptionMetadata.FormatVersion == 2)
                {
                    Logger.Debug(
                        "Starting encrypted file transfer for {FileExternalId} using AES-256-GCM",
                        s3FileKey.FileExternalId);

                    var encryptedRange = Aes256GcmStreamingV2.EncryptedBytesRangeCalculator.FromUnencryptedRange(
                        unencryptedRange: range,
                        unencryptedFileSize: fileSizeInBytes,
                        chainStepsCount: fileEncryptionMetadata.ChainStepSalts.Count);

                    stream.Seek(encryptedRange.FirstSegment.Start, SeekOrigin.Begin);

                    var ikm = KeyDerivationChain.Derive(
                        startingDek: hardDriveStorageClient.GetEncryptionKey(
                            version: fileEncryptionMetadata.KeyVersion,
                            fullEncryptionSession: fullEncryptionSession),
                        stepSalts: fileEncryptionMetadata.ChainStepSalts);

                    await Aes256GcmStreamingV2.DecryptRange(
                        fileAesInputs: fileEncryptionMetadata.ToAesInputsV2(ikm),
                        range: encryptedRange,
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
                    throw new InvalidOperationException(
                        $"Unsupported file encryption format version '{fileEncryptionMetadata.FormatVersion}' " +
                        $"for file '{s3FileKey.FileExternalId}'.");
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

        public void Dispose()
        {
            stream.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            await stream.DisposeAsync();
        }
    }

    private static readonly Serilog.ILogger Logger = Log.ForContext<HardDriveDownloadOperation>();

    public static IFile GetFile(
       S3FileKey s3FileKey,
       FileEncryptionMetadata? fileEncryptionMetadata,
       long fileSizeInBytes,
       FullEncryptionSession? fullEncryptionSession,
       string bucketName,
       HardDriveStorageClient hardDriveStorageClient)
    {
        var filePath = Path.Combine(
            hardDriveStorageClient.Details.FullPath,
            bucketName,
            s3FileKey.FileExternalId.Value);

        if (!File.Exists(filePath))
        {
            Logger.Warning(
                "File not found: {FileExternalId} at path: {FilePath}",
                s3FileKey.FileExternalId,
                filePath);

            throw new FileNotFoundInStorageException(
                $"File '{s3FileKey.FileExternalId}' was not found in Storage '{hardDriveStorageClient.ExternalId}'");
        }

        Logger.Debug(
            "Reading file {FileExternalId} ({FileSize:N0} bytes)",
            s3FileKey.FileExternalId,
            fileSizeInBytes);

        var fileStream = new FileStream(
            path: filePath,
            mode: FileMode.Open,
            access: FileAccess.Read,
            share: FileShare.Read,
            bufferSize: PlikShareStreams.DefaultBufferSize,
            useAsync: true);

        return new HdFile(
            s3FileKey, 
            fileEncryptionMetadata,
            fileSizeInBytes, 
            filePath,
            fullEncryptionSession,
            hardDriveStorageClient, 
            fileStream);
    }

    public static IFile GetFileRange(
        S3FileKey s3FileKey,
        FileEncryptionMetadata fileEncryptionMetadata,
        long fileSizeInBytes,
        BytesRange range,
        FullEncryptionSession? fullEncryptionSession,
        string bucketName,
        HardDriveStorageClient hardDriveStorageClient)
    {
        var filePath = Path.Combine(
            hardDriveStorageClient.Details.FullPath,
            bucketName,
            s3FileKey.FileExternalId.Value);

        if (!File.Exists(filePath))
        {
            Logger.Warning(
                "File not found: {FileExternalId} at path: {FilePath}",
                s3FileKey.FileExternalId,
                filePath);

            throw new FileNotFoundInStorageException(
                $"File '{s3FileKey.FileExternalId}' was not found in Storage '{hardDriveStorageClient.ExternalId}'");
        }

        Logger.Debug(
            "Reading file {FileExternalId} ({FileSize:N0} bytes)",
            s3FileKey.FileExternalId,
            fileSizeInBytes);

        var fileStream = new FileStream(
            path: filePath,
            mode: FileMode.Open,
            access: FileAccess.Read,
            share: FileShare.Read,
            bufferSize: PlikShareStreams.DefaultBufferSize,
            useAsync: true);

        return new HdFileRange(
            s3FileKey: s3FileKey, 
            fileSizeInBytes: fileSizeInBytes, 
            fileEncryptionMetadata: fileEncryptionMetadata, 
            range: range, 
            filePath: filePath, 
            fullEncryptionSession: fullEncryptionSession,
            hardDriveStorageClient: hardDriveStorageClient, 
            stream: fileStream);
    }
}