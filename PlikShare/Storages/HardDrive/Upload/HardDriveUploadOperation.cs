using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Files.Records;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.HardDrive.StorageClient;
using PlikShare.Uploads.Algorithm;
using PlikShare.Uploads.Cache;
using Serilog;

namespace PlikShare.Storages.HardDrive.Upload;

public static class HardDriveUploadOperation
{
    public static async ValueTask<FilePartUploadResult> Execute(
        Memory<byte> fileBytes,
        FileToUploadDetails file,
        FilePartDetails part,
        string bucketName,
        HardDriveStorageClient hardDriveStorage,
        CancellationToken cancellationToken)
    {
        var etag = Guid.NewGuid().ToBase62();
                    
        var filePath = GetFilePath(
            fileExternalId: file.S3FileKey.FileExternalId,
            uploadAlgorithm: part.UploadAlgorithm,
            bucketName: bucketName,
            hardDriveStorage: hardDriveStorage, 
            etag: etag);

        try
        {
            if (file.Encryption.EncryptionType == StorageEncryptionType.Managed)
            {
                var encryptionKey = hardDriveStorage
                    .EncryptionKeyProvider
                    !.GetEncryptionKey(
                        version: file.Encryption.Metadata!.KeyVersion);

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

            await using var fileStream = new FileStream(
                filePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None);
            
            await fileStream.WriteAsync(
                fileBytes,
                cancellationToken);

            Log.Debug("FilePart '{FileExternalId} - {PartNumber} was saved to HardDrive to location {FilePath}'",
                file.S3FileKey.FileExternalId,
                part.Number,
                filePath);

            return new FilePartUploadResult(
                ETag: etag);
        }
        catch (Exception e)
        {
            Log.Error(e,
                "Something went wrong while saving file '{FileExternalId} - {PartNumber} to location {FilePath}'",
                file.S3FileKey.FileExternalId,
                part.Number,
                filePath);

            throw;
        }
    }

    private static string GetFilePath(
        FileExtId fileExternalId,
        UploadAlgorithm uploadAlgorithm,
        string bucketName,
        HardDriveStorageClient hardDriveStorage, 
        string etag)
    {
        return uploadAlgorithm switch
        {
            UploadAlgorithm.DirectUpload => Path.Combine(
                hardDriveStorage.Details.FullPath,
                bucketName,
                $"{fileExternalId}"),

            UploadAlgorithm.MultiStepChunkUpload => Path.Combine(
                hardDriveStorage.Details.FullPath,
                bucketName,
                $"{fileExternalId}.{etag}.part"),

            UploadAlgorithm.SingleChunkUpload => throw new NotSupportedException(
                message:
                $"Upload algorithm '{uploadAlgorithm}' is not supported for {nameof(HardDriveUploadOperation)}"),
            
            _ => throw new ArgumentOutOfRangeException(
                paramName: nameof(uploadAlgorithm),
                message: $"Upload algorithm '{uploadAlgorithm}' is not recognized")
        };
    }
}