using Amazon.S3.Model;
using PlikShare.Core.Clock;
using PlikShare.Core.Encryption;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Exceptions;
using PlikShare.Storages.FileReading;
using PlikShare.Storages.Id;
using PlikShare.Uploads.Algorithm;
using PlikShare.Uploads.Chunking;
using PlikShare.Uploads.Id;
using Serilog;
using System.IO.Pipelines;

namespace PlikShare.Storages.HardDrive.StorageClient;

public class HardDriveStorageClient(
    IMasterDataEncryption masterDataEncryption,
    PreSignedUrlsService preSignedUrlsService,
    IClock clock,
    HardDriveDetailsEntity details,
    int storageId,
    StorageExtId externalId,
    string name,
    StorageEncryption encryption) : IStorageClient
{
    public HardDriveDetailsEntity Details { get; } = details;
    public string Name { get; } = name;
    public StorageEncryption Encryption { get; } = encryption;

    public int StorageId { get; } = storageId;
    public StorageExtId ExternalId { get; } = externalId;

    private string GetBucketPath(string bucketName)
        => Path.Combine(Details.FullPath, bucketName);

    private string GetFilePath(string bucketName, FileExtId fileExternalId)
        => Path.Combine(Details.FullPath, bucketName, fileExternalId.Value);

    public ValueTask DeleteFile(
        string bucketName,
        S3FileKey key,
        CancellationToken cancellationToken = default)
    {
        var bucketPath = GetBucketPath(bucketName);

        if (!Directory.Exists(bucketPath))
        {
            Log.Warning("Bucket directory '{BucketPath}' not found for file '{FileExternalId}'",
                bucketPath, key.FileExternalId);

            return ValueTask.CompletedTask;
        }

        var filePath = GetFilePath(bucketName, key.FileExternalId);

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
        var bucketPath = GetBucketPath(bucketName);

        if (!Directory.Exists(bucketPath))
        {
            Log.Warning("Bucket directory '{BucketPath}' not found.",
                bucketPath);

            return ValueTask.CompletedTask;
        }

        foreach (var key in keys)
        {
            var filePath = GetFilePath(bucketName, key.FileExternalId);

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
        var bucketPath = GetBucketPath(bucketName);

        if (!Directory.Exists(bucketPath))
        {
            Log.Warning("Bucket directory '{BucketPath}' not found for file '{FileExternalId}'",
                bucketPath, key.FileExternalId);

            return;
        }

        var finalFilePath = GetFilePath(bucketName, key.FileExternalId);

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

            var partName = GetPartFileName(
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

    private static string GetPartFileName(FileExtId fileExternalId, string eTag)
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
        WorkspaceEncryptionSession? workspaceEncryptionSession,
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
                WorkspaceDeks = workspaceEncryptionSession.ToWires(masterDataEncryption),
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
        WorkspaceEncryptionSession? workspaceEncryptionSession,
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
                WorkspaceDeks = workspaceEncryptionSession.ToWires(masterDataEncryption)
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

        var bucketPath = GetBucketPath(bucketName);

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
                GetPartFileName(key.FileExternalId, partETag));

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
        var workspacePath = GetBucketPath(bucketName);

        Directory.CreateDirectory(workspacePath);
        return Task.CompletedTask;
    }

    public Task DeleteBucket(
        string bucketName,
        CancellationToken cancellationToken = default)
    {
        var workspacePath = GetBucketPath(bucketName);

        if (Directory.Exists(workspacePath))
        {
            Directory.Delete(workspacePath, recursive: true);
        }

        return Task.CompletedTask;
    }

    public (UploadAlgorithm Algorithm, int FilePartsCount) ResolveUploadAlgorithm(
        long fileSizeInBytes,
        int ikmChainStepsCount)
    {
        var filePartsCount = FileParts.GetTotalNumberOfParts(
            fileSizeInBytes: fileSizeInBytes,
            storageEncryptionType: Encryption.Type,
            ikmChainStepsCount: ikmChainStepsCount);

        return filePartsCount == 1 ?
            (UploadAlgorithm.DirectUpload, filePartsCount) :
            (UploadAlgorithm.MultiStepChunkUpload, filePartsCount);
    }

    public (UploadAlgorithm Algorithm, int FilePartsCount) ResolveCopyUploadAlgorithm(
        long fileSizeInBytes,
        int ikmChainStepsCount)
    {
        //for hard drive, copy upload is the same as normal upload
        return ResolveUploadAlgorithm(fileSizeInBytes, ikmChainStepsCount);
    }

    public string GenerateFileS3KeySecretPart()
    {
        return string.Empty;
    }

    public StorageUploadDetails GetStorageUploadDetails(
        FileUploadExtId fileUploadExternalId,
        long fileSizeInBytes,
        string contentType,
        IUserIdentity userIdentity,
        WorkspaceEncryptionMetadata? workspaceEncryption)
    {
        var fileEncryptionMetadata = this.GenerateFileEncryptionMetadata(
            workspaceEncryption);

        var (algorithm, filePartsCount) = ResolveUploadAlgorithm(
            fileSizeInBytes: fileSizeInBytes,
            ikmChainStepsCount: fileEncryptionMetadata?.ChainStepSalts.Count ?? 0);

        return new StorageUploadDetails
        {
            Algorithm = algorithm,
            FilePartsCount = filePartsCount,

            FileEncryptionMetadata = fileEncryptionMetadata,

            PreSignedUploadLink = null,
            S3UploadId = string.Empty,
            WasMultiPartUploadInitiated = false,
        };
    }

    public ValueTask<IStorageFile> DownloadFile(
        DownloadFileDetails details,
        string bucketName,
        CancellationToken cancellationToken)
    {
        var filePath = GetFilePath(
            bucketName, 
            details.S3FileKey.FileExternalId);

        if (!File.Exists(filePath))
        {
            Log.Warning(
                "File not found: {FileExternalId} at path: {FilePath}",
                details.S3FileKey.FileExternalId,
                filePath);

            throw new FileNotFoundInStorageException(
                $"File '{details.S3FileKey.FileExternalId}' was not found in Storage '{ExternalId}'");
        }

        Log.Debug(
            "Reading file {FileExternalId} ({FileSize:N0} bytes)",
            details.S3FileKey.FileExternalId,
            details.FileSizeInBytes);

        var fileStream = new FileStream(
            path: filePath,
            mode: FileMode.Open,
            access: FileAccess.Read,
            share: FileShare.Read,
            bufferSize: PlikShareStreams.DefaultBufferSize,
            useAsync: true);

        var file = FileEncryption.ReadFile(
            fileSizeInBytes: details.FileSizeInBytes,
            encryptionMode: details.EncryptionMode,
            stream: fileStream,
            enrichLogs: logger => logger
                .ForContext("FileExternalId", details.S3FileKey.FileExternalId)
                .ForContext("FilePath", filePath));

        return ValueTask.FromResult(file);
    }

    public ValueTask<IStorageFile> DownloadFileRange(
        DownloadFileRangeDetails details,
        string bucketName,
        CancellationToken cancellationToken)
    {
        var filePath = GetFilePath(
            bucketName, 
            details.S3FileKey.FileExternalId);

        if (!File.Exists(filePath))
        {
            Log.Warning(
                "File not found: {FileExternalId} at path: {FilePath}",
                details.S3FileKey.FileExternalId,
                filePath);

            throw new FileNotFoundInStorageException(
                $"File '{details.S3FileKey.FileExternalId}' was not found in Storage '{ExternalId}'");
        }

        Log.Debug(
            "Reading file {FileExternalId} ({FileSize:N0} bytes)",
            details.S3FileKey.FileExternalId,
            details.FileSizeInBytes);

        var readPlan = FileEncryption.CalculateRangeReadPlan(
            encryptionMode: details.EncryptionMode,
            fileSizeInBytes: details.FileSizeInBytes,
            range: details.Range);

        var rangedFileStream = new RangedReadOnlyStream(
            inner: new FileStream(
                path: filePath,
                mode: FileMode.Open,
                access: FileAccess.Read,
                share: FileShare.Read,
                bufferSize: PlikShareStreams.DefaultBufferSize,
                useAsync: true),
            start: readPlan.StorageRange.Start,
            length: readPlan.StorageRange.Length,
            leaveOpen: false);

        var file = FileEncryption.ReadFileRange(
            fileSizeInBytes: details.FileSizeInBytes,
            readPlan: readPlan,
            encryptionMode: details.EncryptionMode,
            stream: rangedFileStream,
            enrichLogs: logger => logger
                .ForContext("FileExternalId", details.S3FileKey.FileExternalId)
                .ForContext("FilePath", filePath));

        return ValueTask.FromResult(file);
    }

    public async ValueTask<FilePartUploadResult> UploadFilePart(
        Memory<byte> input,
        UploadFilePartDetails uploadDetails,
        string bucketName,
        CancellationToken cancellationToken)
    {
        var etag = Guid.NewGuid().ToBase62();

        var filePath = GetUploadFilePath(
            fileExternalId: uploadDetails.S3FileKey.FileExternalId,
            uploadAlgorithm: uploadDetails.UploadAlgorithm,
            bucketName: bucketName,
            etag: etag);

        try
        {
            using var filePart = FileEncryption.PrepareFilePartForUpload(
                input: input,
                fileSizeInBytes: uploadDetails.FileSizeInBytes,
                filePart: uploadDetails.Part,
                encryptionMode: uploadDetails.EncryptionMode,
                cancellationToken: cancellationToken);

            await using var fileStream = new FileStream(
                filePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None);

            await fileStream.WriteAsync(
                filePart.Memory,
                cancellationToken);

            Log.Debug("FilePart '{FileExternalId} - {PartNumber} was saved to HardDrive to location {FilePath}'",
                uploadDetails.S3FileKey.FileExternalId,
                uploadDetails.Part.Number,
                filePath);

            return new FilePartUploadResult(
                ETag: etag);
        }
        catch (Exception e)
        {
            Log.Error(e,
                "Something went wrong while saving file '{FileExternalId} - {PartNumber} to location {FilePath}'",
                uploadDetails.S3FileKey.FileExternalId,
                uploadDetails.Part.Number,
                filePath);

            throw;
        }
    }

    public async ValueTask<FilePartUploadResult> UploadFilePart(
        PipeReader input,
        UploadFilePartDetails uploadDetails,
        string bucketName,
        CancellationToken cancellationToken)
    {
        var etag = Guid.NewGuid().ToBase62();

        var filePath = GetUploadFilePath(
            fileExternalId: uploadDetails.S3FileKey.FileExternalId,
            uploadAlgorithm: uploadDetails.UploadAlgorithm,
            bucketName: bucketName,
            etag: etag);

        try
        {
            using var filePart = await FileEncryption.PrepareFilePartForUpload(
                input: input,
                fileSizeInBytes: uploadDetails.FileSizeInBytes,
                filePart: uploadDetails.Part,
                encryptionMode: uploadDetails.EncryptionMode,
                cancellationToken: cancellationToken);

            await using var fileStream = new FileStream(
                filePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None);

            await fileStream.WriteAsync(
                filePart.Memory,
                cancellationToken);
            
            Log.Debug("FilePart '{FileExternalId} - {PartNumber} was saved to HardDrive to location {FilePath}'",
                uploadDetails.S3FileKey.FileExternalId,
                uploadDetails.Part.Number,
                filePath);

            return new FilePartUploadResult(
                ETag: etag);
        }
        catch (Exception e)
        {
            Log.Error(e,
                "Something went wrong while saving file '{FileExternalId} - {PartNumber} to location {FilePath}'",
                uploadDetails.S3FileKey.FileExternalId,
                uploadDetails.Part.Number,
                filePath);

            throw;
        }
    }

    private string GetUploadFilePath(
        FileExtId fileExternalId,
        UploadAlgorithm uploadAlgorithm,
        string bucketName,
        string etag)
    {
        return uploadAlgorithm switch
        {
            UploadAlgorithm.DirectUpload => GetFilePath(bucketName, fileExternalId),

            UploadAlgorithm.MultiStepChunkUpload => Path.Combine(
                GetBucketPath(bucketName),
                GetPartFileName(fileExternalId, etag)),

            UploadAlgorithm.SingleChunkUpload => throw new NotSupportedException(
                message:
                $"Upload algorithm '{uploadAlgorithm}' is not supported for {nameof(HardDriveStorageClient)}"),

            _ => throw new ArgumentOutOfRangeException(
                paramName: nameof(uploadAlgorithm),
                message: $"Upload algorithm '{uploadAlgorithm}' is not recognized")
        };
    }
}