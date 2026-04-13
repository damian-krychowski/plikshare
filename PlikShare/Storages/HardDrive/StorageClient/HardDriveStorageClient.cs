using Amazon.S3.Model;
using PlikShare.Core.Clock;
using PlikShare.Core.Encryption;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;
using PlikShare.Uploads.Algorithm;
using PlikShare.Uploads.Chunking;
using PlikShare.Uploads.Id;
using Serilog;

namespace PlikShare.Storages.HardDrive.StorageClient;

public class HardDriveStorageClient(
    PreSignedUrlsService preSignedUrlsService,
    IClock clock,
    HardDriveDetailsEntity details,
    int storageId,
    StorageExtId externalId,
    string name,
    StorageEncryptionType encryptionType,
    StorageEncryptionDetails? encryptionDetails) : IStorageClient
{
    public HardDriveDetailsEntity Details { get; } = details;
    public string Name { get; } = name;
    public StorageEncryptionType EncryptionType { get; } = encryptionType;
    public StorageEncryptionDetails? EncryptionDetails { get; private set; } = encryptionDetails;
    public EncryptionKeyProvider? EncryptionKeyProvider { get; private set; } = StorageEncryptionExtensions.PrepareEncryptionKeyProvider(
        encryptionDetails: encryptionDetails);

    public int StorageId { get; } = storageId;
    public StorageExtId ExternalId { get; } = externalId;

    public void SetEncryptionDetails(StorageEncryptionDetails? newEncryptionDetails)
    {
        // Build the derived provider first, then swap both fields. Reads happen
        // on many threads; reference-writes are atomic in .NET so a concurrent
        // read sees either the old pair or the new one consistently enough —
        // and a mid-swap cross-read would only cause an AES-GCM tag mismatch,
        // which downstream already handles as an invalid-session error.
        var newProvider = StorageEncryptionExtensions.PrepareEncryptionKeyProvider(
            encryptionDetails: newEncryptionDetails);

        EncryptionKeyProvider = newProvider;
        EncryptionDetails = newEncryptionDetails;
    }

    public ValueTask DeleteFile(
        string bucketName, 
        S3FileKey key, 
        CancellationToken cancellationToken = default)
    {
        var bucketPath = Path.Combine(
            Details.FullPath,
            bucketName);

        if (!Directory.Exists(bucketPath))
        {
            Log.Warning("Bucket directory '{BucketPath}' not found for file '{FileExternalId}'", 
                bucketPath, key.FileExternalId);

            return ValueTask.CompletedTask; 
        }

        var filePath = Path.Combine(
            bucketPath, 
            key.FileExternalId.Value);

        if (!File.Exists(filePath))
        {
            Log.Warning("File '{FilePath}' not found for '{FileExternalId}'", 
                filePath, key.FileExternalId);

            return ValueTask.CompletedTask;
        }
        
        try
        {
            File.Delete(filePath);

            Log.Information("File '{FileExternalId}' was deleted.", 
                key.FileExternalId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to delete file '{FilePath}' for '{FileExternalId}'", 
                filePath, key.FileExternalId);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask DeleteFiles(
        string bucketName,
        S3FileKey[] keys,
        CancellationToken cancellationToken = default)
    {
        var bucketPath = Path.Combine(
            Details.FullPath,
            bucketName);

        if (!Directory.Exists(bucketPath))
        {
            Log.Warning("Bucket directory '{BucketPath}' not found.",
                bucketPath);

            return ValueTask.CompletedTask;
        }

        foreach (var key in keys)
        {
            var filePath = Path.Combine(
                bucketPath,
                key.FileExternalId.Value);

            if (!File.Exists(filePath))
            {
                Log.Warning("File '{FilePath}' not found for '{FileExternalId}'",
                    filePath, key.FileExternalId);

                continue;
            }

            try
            {
                File.Delete(filePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to delete file '{FilePath}' for '{FileExternalId}'",
                    filePath, key.FileExternalId);
            }
        }
        

        Log.Information("Files ({FilesCount}) in bucket '{BucketPath}' were deleted.",
            keys.Length,
            bucketPath);

        return ValueTask.CompletedTask;
    }

    public async Task CompleteMultiPartUpload(
        string bucketName, 
        S3FileKey key,
        string uploadId, 
        List<PartETag> partETags,
        CancellationToken cancellationToken = default)
    {
        var bucketPath = Path.Combine(
            Details.FullPath,
            bucketName);

        if (!Directory.Exists(bucketPath))
        {
            Log.Warning("Bucket directory '{BucketPath}' not found for file '{FileExternalId}'", 
                bucketPath, key.FileExternalId);

            return;
        }

        var finalFilePath = Path.Combine(
            bucketPath,
            key.FileExternalId.Value);

        try
        {
            var sorterPartETags = partETags
                .OrderBy(part => part.PartNumber)
                .ToList();

            await MergeParts(
                bucketPath,
                key.FileExternalId,
                finalFilePath,
                sorterPartETags,
                cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to merge parts or delete part files for '{FileExternalId}' in '{BucketName}'", 
                key.FileExternalId, bucketName);

            // If merging failed, we should not leave a partially written file
            if (File.Exists(finalFilePath))
            {
                File.Delete(finalFilePath);
            }

            //todo that should propagate upward to the user somehow

            return;
        }

        Log.Information("Completed MultiPartUpload for '{FileExternalId}' in '{BucketName}' of Storage '{StorageId}'",
            key.FileExternalId, bucketName, StorageId);
    }
    

    //use Pipe to perform those operations 
    private static async Task MergeParts(
        string bucketPath,
        FileExtId fileExternalId,
        string finalFilePath,
        List<PartETag> sorterPartETags,
        CancellationToken cancellationToken)
    {
        await using var outputStream = new FileStream(
            finalFilePath, 
            FileMode.Create,
            access: FileAccess.Write,
            share: FileShare.None,
            bufferSize: PlikShareStreams.DefaultBufferSize);

        foreach (var partETag in sorterPartETags)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var partName = GetFileName(
                fileExternalId, 
                partETag.ETag);

            var partFilePath = Path.Combine(
                bucketPath, 
                partName);

            await CopyPartIntoDestination(
                partFilePath,
                outputStream,
                cancellationToken);

            File.Delete(partFilePath);

            Log.Debug("Merged and deleted part file: '{FileName}'",
                partName);
        }
    }

    private static string GetFileName(FileExtId fileExternalId, string eTag)
    {
        return $"{fileExternalId}.{eTag}.part";
    }
    
    private static async Task CopyPartIntoDestination(
        string partFilePath,
        FileStream outputStream,
        CancellationToken cancellationToken)
    {
        await using var inputStream = new FileStream(
            path: partFilePath, 
            mode: FileMode.Open,
            bufferSize: PlikShareStreams.DefaultBufferSize,
            access: FileAccess.Read,
            share: FileShare.None);

        await inputStream.CopyToAsync(
            outputStream, 
            cancellationToken);
    }

    public ValueTask<PreSignedUploadLinkResult> GetPreSignedUploadFilePartLink(
        string bucketName,
        FileUploadExtId fileUploadExternalId,
        S3FileKey key,
        string uploadId,
        int partNumber,
        string contentType,
        int? boxLinkId,
        IUserIdentity userIdentity,
        bool enforceInternalPassThrough,
        FullEncryptionSession? fullEncryptionSession,
        CancellationToken cancellationToken)
    {
        var url = preSignedUrlsService.GeneratePreSignedUploadUrl(
            payload: new PreSignedUrlsService.UploadPayload
            {
                FileUploadExternalId = fileUploadExternalId,
                PartNumber = partNumber,
                ContentType = contentType,
                PreSignedBy = new PreSignedUrlsService.PreSignedUrlOwner
                {
                    Identity = userIdentity.Identity,
                    IdentityType = userIdentity.IdentityType
                },
                ExpirationDate = clock.UtcNow.Add(TimeSpan.FromMinutes(1)),
                BoxLinkId = boxLinkId,
                Kek = fullEncryptionSession?.Kek
            });

        var result = new PreSignedUploadLinkResult
        {
            Url = url,
            IsCompleteFilePartUploadCallbackRequired = false
        };

        return ValueTask.FromResult(result);
    }

    public ValueTask<string> GetPreSignedDownloadFileLink(
        string bucketName,
        S3FileKey key,
        string contentType,
        string fileName,
        ContentDispositionType contentDisposition,
        int? boxLinkId,
        IUserIdentity userIdentity,
        bool enforceInternalPassThrough,
        FullEncryptionSession? fullEncryptionSession,
        CancellationToken cancellationToken)
    {
        var result = preSignedUrlsService.GeneratePreSignedDownloadUrl(
            payload: new PreSignedUrlsService.DownloadPayload
            {
                FileExternalId = key.FileExternalId,
                PreSignedBy = new PreSignedUrlsService.PreSignedUrlOwner
                {
                    Identity = userIdentity.Identity,
                    IdentityType = userIdentity.IdentityType
                },
                ContentDisposition = contentDisposition,
                ExpirationDate = clock.UtcNow.Add(TimeSpan.FromDays(1)),
                BoxLinkId = boxLinkId,
                Kek = fullEncryptionSession?.Kek
            });

        return ValueTask.FromResult(result);
    }

    public Task AbortMultiPartUpload(
        string bucketName, 
        S3FileKey key, 
        string uploadId,
        List<string> partETags,
        CancellationToken cancellationToken = default)
    {
        //no parts were created, there is nothing to delete
        if (partETags.Count == 0)
        {
            Log.Information("Successfully aborted MultiPartUpload for '{FileExternalId}' in '{BucketName}' of Storage '{StorageId}'",
                key.FileExternalId,
                bucketName,
                StorageId);

            return Task.CompletedTask;
        }

        var bucketPath = Path.Combine(
            Details.FullPath,
            bucketName);

        if (!Directory.Exists(bucketPath))
        {
            Log.Warning("Bucket directory '{BucketPath}' not found for file '{FileExternalId}'",
                bucketPath, key.FileExternalId);

            return Task.CompletedTask;
        }
        
        foreach (var partETag in partETags)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.CompletedTask;

            var file = Path.Combine(
                bucketPath,
                GetFileName(key.FileExternalId, partETag));

            try
            {
                File.Delete(file);

                Log.Debug("Deleted part file '{File}' for '{FileExternalId}'",
                    file, key.FileExternalId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to delete part file '{File}' for '{FileExternalId}'",
                    file, key.FileExternalId);
            }
        }

        Log.Information("Successfully aborted MultiPartUpload for '{FileExternalId}' in '{BucketName}' of Storage '{StorageId}'. Deleted parts: {DeletedPartsCount}",
            key.FileExternalId,
            bucketName,
            StorageId,
            partETags.Count);

        return Task.CompletedTask;
    }

    public Task CreateBucketIfDoesntExist(
        string bucketName, 
        CancellationToken cancellationToken = default)
    {
        var workspacePath = Path.Combine(
            Details.FullPath, 
            bucketName);
        
        Directory.CreateDirectory(workspacePath);
        return Task.CompletedTask;
    }

    public Task DeleteBucket(
        string bucketName, 
        CancellationToken cancellationToken = default)
    {
        var workspacePath = Path.Combine(Details.FullPath, bucketName);

        if (Directory.Exists(workspacePath))
        {
            Directory.Delete(workspacePath, recursive: true);
        }

        return Task.CompletedTask;
    }
    
    public (UploadAlgorithm Algorithm, int FilePartsCount) ResolveUploadAlgorithm(
        long fileSizeInBytes)
    {
        var filePartsCount = FileParts.GetTotalNumberOfParts(
            fileSizeInBytes: fileSizeInBytes,
            storageEncryptionType: EncryptionType);

        return filePartsCount == 1 ? 
            (UploadAlgorithm.DirectUpload, filePartsCount) : 
            (UploadAlgorithm.MultiStepChunkUpload, filePartsCount);
    }

    public (UploadAlgorithm Algorithm, int FilePartsCount) ResolveCopyUploadAlgorithm(
        long fileSizeInBytes)
    {
        //for hard drive, copy upload is the same as normal upload
        return ResolveUploadAlgorithm(fileSizeInBytes);
    }

    public string GenerateFileS3KeySecretPart()
    {
        return string.Empty;
    }

    public StorageUploadDetails GetStorageUploadDetails(
        FileUploadExtId fileUploadExternalId,
        long fileSizeInBytes,
        string contentType,
        IUserIdentity userIdentity)
    {
        var (algorithm, filePartsCount) = ResolveUploadAlgorithm(
            fileSizeInBytes: fileSizeInBytes);

        return new StorageUploadDetails
        {
            Algorithm = algorithm,
            FilePartsCount = filePartsCount,

            FileEncryption = this.GenerateFileEncryptionDetails(),

            PreSignedUploadLink = null,
            S3UploadId = string.Empty,
            WasMultiPartUploadInitiated = false,
        };
    }
}