using PlikShare.Core.Encryption;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Files.Records;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.HardDrive.Upload;
using PlikShare.Uploads.Algorithm;
using PlikShare.Uploads.Cache;
using Serilog;

namespace PlikShare.Storages.S3.Upload;

public class S3UploadOperation
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<S3UploadOperation>();
    
    public static async ValueTask<FilePartUploadResult> Execute(
        Memory<byte> fileBytes,
        FileToUploadDetails file,
        FilePartDetails part,
        string bucketName,
        S3StorageClient s3StorageClient,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        Logger.Debug(
            "Starting upload operation for file {FileExternalId} part {PartNumber} to bucket {BucketName} with encryption {EncryptionType}",
            file.S3FileKey.FileExternalId,
            part.Number,
            bucketName,
            file.Encryption.EncryptionType);

        try
        {
            if (file.Encryption.EncryptionType == StorageEncryptionType.Managed)
            {
                var encryptionKey = s3StorageClient
                    .EncryptionKeyProvider!
                    .GetEncryptionKey(version: file.Encryption.Metadata!.KeyVersion);

                Aes256GcmStreaming.EncryptFilePartInPlace(
                    key: encryptionKey,
                    salt: file.Encryption.Metadata!.Salt,
                    noncePrefix: file.Encryption.Metadata.NoncePrefix,
                    partNumber: part.Number,
                    partSizeInBytes: part.SizeInBytes,
                    fullFileSizeInBytes: file.SizeInBytes,
                    inputOutputBuffer: fileBytes,
                    cancellationToken: cancellationToken);
            }
            
            var etag = await UploadToS3(
                partNumber: part.Number,
                s3StorageClient: s3StorageClient,
                s3FileKey: file.S3FileKey,
                s3UploadId: file.S3UploadId,
                bucketName: bucketName,
                fileBytes: fileBytes,
                uploadAlgorithm: part.UploadAlgorithm,
                cancellationToken: cancellationToken);

            var duration = DateTime.UtcNow - startTime;

            Logger.Debug(
                "Successfully uploaded part {PartNumber} of file {FileExternalId} in {DurationMs}ms (ETag: {ETag})",
                part.Number,
                file.S3FileKey.FileExternalId,
                duration.TotalMilliseconds,
                etag);

            return new FilePartUploadResult(ETag: etag);
        }
        catch (OperationCanceledException)
        {
            Logger.Warning(
                "Upload operation cancelled for file {FileExternalId} part {PartNumber}",
                file.S3FileKey.FileExternalId,
                part.Number);

            throw;
        }
        catch (Exception e)
        {
            Logger.Error(
                e,
                "Failed to upload part {PartNumber} of file {FileExternalId}. Error: {ErrorMessage}",
                part.Number,
                file.S3FileKey.FileExternalId,
                e.Message);

            throw;
        }
    }

    private static async Task<string> UploadToS3(
        int partNumber,
        S3StorageClient s3StorageClient,
        S3FileKey s3FileKey,
        string s3UploadId,
        string bucketName,
        ReadOnlyMemory<byte> fileBytes,
        UploadAlgorithm uploadAlgorithm,
        CancellationToken cancellationToken)
    {
        try
        {
            return uploadAlgorithm switch
            {
                UploadAlgorithm.DirectUpload => await UploadWholeFile(
                    s3StorageClient: s3StorageClient,
                    s3FileKey: s3FileKey,
                    bucketName: bucketName,
                    fileBytes: fileBytes,
                    cancellationToken: cancellationToken),

                UploadAlgorithm.MultiStepChunkUpload => await UploadPart(
                    partNumber: partNumber, 
                    s3StorageClient: s3StorageClient, 
                    s3FileKey: s3FileKey, 
                    s3UploadId: s3UploadId, 
                    bucketName: bucketName, 
                    fileBytes: fileBytes, 
                    cancellationToken: cancellationToken),

                UploadAlgorithm.SingleChunkUpload => throw new NotSupportedException(
                    message:
                    $"Upload algorithm '{uploadAlgorithm}' is not supported for {nameof(HardDriveUploadOperation)}"),

                _ => throw new ArgumentOutOfRangeException(
                    paramName: nameof(uploadAlgorithm),
                    message: $"Upload algorithm '{uploadAlgorithm}' is not recognized")
            };
        }
        catch (Exception e)
        {
            if (uploadAlgorithm == UploadAlgorithm.MultiStepChunkUpload)
            {
                Logger.Error(
                    e,
                    "S3 upload failed for file {FileExternalId} part {PartNumber}. Error: {ErrorMessage}",
                    s3FileKey.FileExternalId,
                    partNumber,
                    e.Message);
            }

            if (uploadAlgorithm == UploadAlgorithm.DirectUpload)
            {
                Logger.Error(
                    e,
                    "S3 direct upload failed for file {FileExternalId}. Error: {ErrorMessage}",
                    s3FileKey.FileExternalId,
                    e.Message);
            }

            throw;
        }
    }

    private static async Task<string> UploadPart(
        int partNumber, 
        S3StorageClient s3StorageClient,
        S3FileKey s3FileKey,
        string s3UploadId,
        string bucketName,
        ReadOnlyMemory<byte> fileBytes,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        Logger.Debug(
            "Initiating S3 upload for file {FileExternalId} part {PartNumber} (Size: {PartSize} bytes, UploadId: {UploadId})",
            s3FileKey.FileExternalId,
            partNumber,
            fileBytes.Length,
            s3UploadId);

        var etag = await s3StorageClient.UploadPart(
            fileBytes: fileBytes,
            bucketName: bucketName,
            key: s3FileKey,
            uploadId: s3UploadId,
            partNumber: partNumber,
            cancellationToken: cancellationToken);

        var duration = DateTime.UtcNow - startTime;

        Logger.Debug(
            "Completed S3 upload for file {FileExternalId} part {PartNumber} in {DurationMs}ms (ETag: {ETag}, Speed: {SpeedMBps:F2} MB/s)",
            s3FileKey.FileExternalId,
            partNumber,
            duration.TotalMilliseconds,
            etag,
            fileBytes.Length / 1024.0 / 1024.0 / duration.TotalSeconds);

        return etag;
    }
    
    private static async Task<string> UploadWholeFile(
        S3StorageClient s3StorageClient,
        S3FileKey s3FileKey,
        string bucketName,
        ReadOnlyMemory<byte> fileBytes,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        Logger.Debug(
            "Initiating S3 direct upload for file {FileExternalId} (Size: {FileSize} bytes)",
            s3FileKey.FileExternalId,
            fileBytes.Length);
        
        var etag = await s3StorageClient.UploadFile(
            fileBytes: fileBytes,
            bucketName: bucketName,
            key: s3FileKey,
            cancellationToken: cancellationToken);

        var duration = DateTime.UtcNow - startTime;

        Logger.Debug(
            "Completed S3 direct upload for file {FileExternalId} n {DurationMs}ms (ETag: {ETag}, Speed: {SpeedMBps:F2} MB/s)",
            s3FileKey.FileExternalId,
            duration.TotalMilliseconds,
            etag,
            fileBytes.Length / 1024.0 / 1024.0 / duration.TotalSeconds);

        return etag;
    }
}