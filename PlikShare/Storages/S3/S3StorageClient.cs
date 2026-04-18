using Amazon.S3;
using Amazon.S3.Model;
using CommunityToolkit.HighPerformance;
using PlikShare.Core.Clock;
using PlikShare.Core.Encryption;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Files.PreSignedLinks.RangeRequests;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.FileReading;
using PlikShare.Storages.Id;
using PlikShare.Uploads.Algorithm;
using PlikShare.Uploads.Chunking;
using PlikShare.Uploads.Id;
using Serilog;
using System.IO.Pipelines;

namespace PlikShare.Storages.S3;

public class S3StorageClient(
    string appUrl,
    IMasterDataEncryption masterDataEncryption,
    IClock clock,
    IAmazonS3 s3Client,
    int storageId,
    StorageExtId externalId,
    string name,
    PreSignedUrlsService preSignedUrlsService,
    StorageEncryption encryption) : IStorageClient, IDisposable
{
    public const int MicroFileThreshold = 1 * SizeInBytes.Mb; //1MB

    public int StorageId { get; } = storageId;
    public StorageExtId ExternalId { get; } = externalId;
    public string Name { get; } = name;
    private readonly RateLimiter _rateLimiter = new(100, 80);
    
    public StorageEncryption Encryption { get; } = encryption;
    

    public async ValueTask DeleteFile(
        string bucketName,
        S3FileKey key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new DeleteObjectRequest()
            {
                BucketName = bucketName,
                Key = key.Value
            };
            
            await s3Client.DeleteObjectAsync(
                request: request,
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            Log.Error(e, "[S3] Something went wrong while deleting a file '{BucketName}/{Key}'",
                bucketName,
                key);
            
            throw;
        }
    }

    public async ValueTask DeleteFiles(
        string bucketName,
        S3FileKey[] keys,
        CancellationToken cancellationToken = default)
    {
        if (keys.Length > 1000)
            throw new ArgumentOutOfRangeException(
                "Maximum allowed number of keys to delete at once is 1000 but found: " + keys.Length);

        try
        {
            var request = new DeleteObjectsRequest()
            {
                BucketName = bucketName,
                Objects = keys
                    .Select(k => new KeyVersion
                    {
                        Key = k.Value
                    })
                    .ToList()
            };

            var result = await s3Client.DeleteObjectsAsync(
                request: request,
                cancellationToken: cancellationToken);
            
            //todo how to handle errors?
        }
        catch (Exception e)
        {
            Log.Error(e, "[S3] Something went wrong while deleting files '{BucketName}: {Keys}'",
                bucketName,
                keys);

            throw;
        }
    }

    public async Task CompleteMultiPartUpload(
        string bucketName,
        S3FileKey key,
        string uploadId,
        List<PartETag> partETags,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new CompleteMultipartUploadRequest
            {
                BucketName = bucketName,
                Key = key.Value,
                UploadId = uploadId,
                PartETags = partETags
            };

            await s3Client.CompleteMultipartUploadAsync(
                request: request,
                cancellationToken: cancellationToken);

            Log.Information("[S3] MultiPartFileUpload '{BucketName}/{Key}' uploadId: {S3UploadId} was completed",
                bucketName,
                key,
                uploadId);
        }
        catch (Exception e)
        {
            Log.Error(e, 
                "[S3] Something went wrong while completing a MultiPartFileUpload '{BucketName}/{Key}' uploadId: '{S3UploadId}'",
                bucketName, key, uploadId);
            
            throw;
        }
    }
    
    public async ValueTask<PreSignedUploadLinkResult> GetPreSignedUploadFilePartLink(
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
        CancellationToken cancellationToken = default)
    {
        if (Encryption is ManagedStorageEncryption or FullStorageEncryption || enforceInternalPassThrough)
        {
            var url = preSignedUrlsService.GeneratePreSignedUploadUrl(
                new PreSignedUrlsService.UploadPayload
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
                    WorkspaceDeks = workspaceEncryptionSession?.Entries.ToWires(masterDataEncryption) ?? []
                });

            return new PreSignedUploadLinkResult
            {
                Url = url,
                IsCompleteFilePartUploadCallbackRequired = false
            };
        }

        if (Encryption is NoStorageEncryption)
        {
            var url = await GetDirectS3PreSignedUploadFilePartLink(
                bucketName,
                key,
                uploadId,
                partNumber,
                contentType);

            return new PreSignedUploadLinkResult
            {
                Url = url,
                IsCompleteFilePartUploadCallbackRequired = true
            };
        }

        throw new NotImplementedException($"Unknown encryption type: '{Encryption.GetType()}'");
    }

    public async Task<string> GetDirectS3PreSignedUploadFilePartLink(
        string bucketName, 
        S3FileKey key, 
        string uploadId,
        int partNumber, 
        string contentType)
    {
        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = bucketName,
                Key = key.Value,
                UploadId = uploadId,
                PartNumber = partNumber,
                ContentType = contentType,
                Verb = HttpVerb.PUT,
                Expires = clock.Now.Add(TimeSpan.FromMinutes(15)),
            };
            
            var response = await s3Client.GetPreSignedURLAsync(
                request: request);

            return response;
        }
        catch (Exception e)
        {
            Log.Error(e, 
                "[S3] Something went wrong while getting pre-signed url for an upload of a multi part file '{BucketName}/{Key}' number: '{PartNumber}', uploadId: '{S3UploadId}'",
                bucketName, key, partNumber, uploadId);
            throw;
        }
    }
    
    public async Task<string> GetPreSignedUploadFullFileLink(
        string bucketName,
        S3FileKey key,
        string contentType,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = bucketName,
                Key = key.Value,
                ContentType = contentType,
                Verb = HttpVerb.PUT,
                Expires = clock.Now.Add(TimeSpan.FromMinutes(15)),
            };

            var response = await s3Client.GetPreSignedURLAsync(request);

            return response;
        }
        catch (Exception e)
        {
            Log.Error(e,
                "[S3] Something went wrong while getting pre-signed url for file upload '{BucketName}/{Key}'",
                bucketName, key);

            throw;
        }
    }

    public async ValueTask<string> GetPreSignedDownloadFileLink(
        string bucketName,
        S3FileKey key,
        string contentType,
        string fileName,
        ContentDispositionType contentDisposition,
        int? boxLinkId,
        IUserIdentity userIdentity,
        bool enforceInternalPassThrough,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        CancellationToken cancellationToken = default)
    {
        if (Encryption is ManagedStorageEncryption or FullStorageEncryption || enforceInternalPassThrough)
        {
            return preSignedUrlsService.GeneratePreSignedDownloadUrl(
                new PreSignedUrlsService.DownloadPayload
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
                    WorkspaceDeks = workspaceEncryptionSession?.Entries.ToWires(masterDataEncryption) ?? []
                });
        }

        if (Encryption is NoStorageEncryption)
        {
            return await GetDirectS3PreSignedDownloadLink(
                bucketName: bucketName,
                key: key,
                contentType: contentType,
                contentDisposition: contentDisposition,
                fileName: fileName);
        }
        
        throw new NotImplementedException($"Unknown encryption type: '{Encryption.GetType()}'");
    }

    private async Task<string> GetDirectS3PreSignedDownloadLink(
        string bucketName,
        S3FileKey key,
        string contentType,
        ContentDispositionType contentDisposition,
        string fileName)
    {
        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = bucketName,
                Key = key.Value,
                Verb = HttpVerb.GET,
                Expires = clock.Now.Add(TimeSpan.FromHours(3)),
                ResponseHeaderOverrides = new ResponseHeaderOverrides
                {
                    ContentType = contentType,
                    ContentDisposition = ContentDispositionHelper.CreateContentDisposition(
                        fileName: fileName,
                        disposition: contentDisposition)
                }
            };
            
            var response = await s3Client.GetPreSignedURLAsync(
                request: request);

            return response;
        }
        catch (Exception e)
        {
            Log.Error(e, "[S3] Something went wrong while getting pre-signed url to download a file '{BucketName}/{Key}'",
                bucketName, key);
            throw;
        }
    }

    public async Task AbortMultiPartUpload(
        string bucketName,
        S3FileKey key,
        string uploadId,
        List<string> partETags,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await s3Client.AbortMultipartUploadAsync(
                request: new AbortMultipartUploadRequest
                {
                    BucketName = bucketName,
                    Key = key.Value,
                    UploadId = uploadId
                },
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            Log.Error(e,
                "[S3] Something went wrong while aborting a multi part upload '{BucketName}/{Key}', uploadId: '{S3UploadId}'",
                bucketName, key, uploadId);

            throw;
        }
    }

    public async ValueTask<InitiatedUpload> InitiateMultiPartUpload(
        string bucketName,
        S3FileKey key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request =  new InitiateMultipartUploadRequest
            {
                BucketName = bucketName,
                Key = key.Value
            };
            
            var result = await s3Client.InitiateMultipartUploadAsync(
                request: request,
                cancellationToken: cancellationToken);

            return new InitiatedUpload(
                S3UploadId: result.UploadId);
        }
        catch (Exception e)
        {
            Log.Error(e, "[S3] Something went wrong while initiating a multi part upload '{BucketName}/{Key}'",
                bucketName, key);
            throw;
        }
    }

    public async Task CreateBucketIfDoesntExist(
        string bucketName,
        CancellationToken cancellationToken = default)
    {
        await CreateBucketIfDoesNotExist(
            bucketName: bucketName,
            cancellationToken: cancellationToken);

        await PutBucketCORS(
            bucketName: bucketName,
            cancellationToken: cancellationToken);
    }
    
    public async Task CreateBucketIfDoesNotExist(
        string bucketName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await s3Client.PutBucketAsync(new PutBucketRequest
            {
                BucketName = bucketName
            }, cancellationToken);
            
            Log.Information("[S3] Bucket '{BucketName}' was created.", bucketName);
        }
        catch (BucketAlreadyOwnedByYouException e)
        {
            Log.Warning(e, "[S3] Bucket '{BucketName}' already exists", bucketName);
        }
        catch (Exception e)
        {
            Log.Error(e, "[S3] Something went wrong while creating a bucket '{BucketName}'", bucketName);
            
            throw;
        }
    }

    public async Task PutBucketCORS(
        string bucketName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await s3Client.PutCORSConfigurationAsync(new PutCORSConfigurationRequest
            {
                BucketName = bucketName,
                Configuration = new CORSConfiguration
                {
                    Rules =
                    [
                        new CORSRule
                        {
                            AllowedMethods = ["GET", "PUT"],
                            AllowedHeaders = ["*"],
                            ExposeHeaders = ["Etag"],
                            AllowedOrigins = [appUrl]
                        }
                    ]
                }
            }, cancellationToken);
            
            
            Log.Information("[S3] CORS for Bucket '{BucketName}' was set. AllowedOrigins '{AlloweOrigins}'",
                bucketName,
                appUrl);
        }
        catch (Exception e)
        {
            Log.Error(e, "[S3] Something went wrong while putting CORS to a bucket '{BucketName}'", bucketName);
            throw;
        }
    }
    
    public async Task DeleteBucket(
        string bucketName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await s3Client.DeleteBucketAsync(
                bucketName: bucketName,
                cancellationToken: cancellationToken);
            
            Log.Information("[S3] Bucket '{BucketName}' was deleted.", bucketName);
        }
        catch (AmazonS3Exception e) when (e.ErrorCode == "NoSuchBucket")
        {
            Log.Warning("[S3] Bucket '{BucketName}' was not found. Delete operation was skipped.", bucketName);
        }
        catch (Exception e)
        {
            Log.Error(e, "[S3] Something went wrong while deleting a bucket '{BucketName}'", bucketName);
            throw;
        }
    }

    public (UploadAlgorithm Algorithm, int FilePartsCount) ResolveCopyUploadAlgorithm(
        long fileSizeInBytes,
        int ikmChainStepsCount)
    {
        var filePartsCount = FileParts.GetTotalNumberOfParts(
            fileSizeInBytes: fileSizeInBytes,
            storageEncryptionType: Encryption.Type,
            ikmChainStepsCount: ikmChainStepsCount);

        return filePartsCount == 1
            ? (UploadAlgorithm.DirectUpload, filePartsCount)
            : (UploadAlgorithm.MultiStepChunkUpload, filePartsCount);
    }

    public string GenerateFileS3KeySecretPart()
    {
        return Guid.NewGuid().ToBase62();
    }

    public (UploadAlgorithm Algorithm, int FilePartsCount) ResolveUploadAlgorithm(
        long fileSizeInBytes,
        int ikmChainStepsCount)
    {
        var filePartsCount = FileParts.GetTotalNumberOfParts(
            fileSizeInBytes: fileSizeInBytes,
            storageEncryptionType: Encryption.Type,
            ikmChainStepsCount: ikmChainStepsCount);

        if (Encryption is ManagedStorageEncryption or FullStorageEncryption)
        {
            return filePartsCount == 1
                ? (UploadAlgorithm.DirectUpload, filePartsCount)
                : (UploadAlgorithm.MultiStepChunkUpload, filePartsCount);
        }

        if (filePartsCount == 1)
        {
            return fileSizeInBytes <= MicroFileThreshold
                ? (UploadAlgorithm.DirectUpload, filePartsCount)
                : (UploadAlgorithm.SingleChunkUpload, filePartsCount);
        }

        return (UploadAlgorithm.MultiStepChunkUpload, filePartsCount);
    }

    public async Task<Stream> GetFile(
        string bucketName,
        S3FileKey key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = key.Value
            };

            var response = await s3Client.GetObjectAsync(
                request: request,
                cancellationToken: cancellationToken);

            return response.ResponseStream;
        }
        catch (Exception e)
        {
            Log.Error(e, "[S3] Something went wrong while getting a file '{BucketName}/{Key}'",
                bucketName,
                key);

            throw;
        }
    }

    public async Task<Stream> GetFileRange(
        string bucketName,
        S3FileKey key,
        BytesRange range,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = key.Value,
                ByteRange = new ByteRange(range.Start, range.End)
            };

            var response = await s3Client.GetObjectAsync(
                request: request,
                cancellationToken: cancellationToken);

            return response.ResponseStream;
        }
        catch (Exception e)
        {
            Log.Error(
                e,
                "[S3] Something went wrong while getting file range '{BucketName}/{Key}' (range: {Range})",
                bucketName,
                key,
                $"{range.Start}-{range.End}");

            throw;
        }
    }

    public async ValueTask<IStorageFile> DownloadFile(
        DownloadFileDetails details,
        string bucketName,
        CancellationToken cancellationToken)
    {
        Log.Debug(
            "Requesting file stream from S3 for {FileExternalId} at {S3Key}",
            details.S3FileKey.FileExternalId,
            details.S3FileKey.Value);

        var stream = await GetFile(
            bucketName: bucketName,
            key: details.S3FileKey,
            cancellationToken: cancellationToken);

        return FileEncryption.ReadFile(
            fileSizeInBytes: details.FileSizeInBytes,
            encryptionMode: details.EncryptionMode,
            stream: stream,
            enrichLogs: logger => logger
                .ForContext("FileExternalId", details.S3FileKey.FileExternalId)
                .ForContext("BucketName", bucketName)
                .ForContext("S3Key", details.S3FileKey.Value));
    }

    public async ValueTask<IStorageFile> DownloadFileRange(
        DownloadFileRangeDetails details,
        string bucketName,
        CancellationToken cancellationToken)
    {
        Log.Debug(
            "Requesting ranged ({Range}) file stream from S3 for {FileExternalId} at {S3Key}",
            details.Range,
            details.S3FileKey.FileExternalId,
            details.S3FileKey.Value);

        var readPlan = FileEncryption.CalculateRangeReadPlan(
            encryptionMode: details.EncryptionMode,
            fileSizeInBytes: details.FileSizeInBytes,
            range: details.Range);

        var stream = await GetFileRange(
            bucketName: bucketName,
            key: details.S3FileKey,
            range: readPlan.StorageRange,
            cancellationToken: cancellationToken);

        return FileEncryption.ReadFileRange(
            fileSizeInBytes: details.FileSizeInBytes,
            readPlan: readPlan,
            encryptionMode: details.EncryptionMode,
            stream: stream,
            enrichLogs: logger => logger
                .ForContext("FileExternalId", details.S3FileKey.FileExternalId)
                .ForContext("BucketName", bucketName)
                .ForContext("S3Key", details.S3FileKey.Value));
    }

    public async ValueTask<FilePartUploadResult> UploadFilePart(
        Memory<byte> input,
        UploadFilePartDetails uploadDetails,
        string bucketName,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        Log.Debug(
            "Starting upload operation for file {FileExternalId} part {PartNumber} to bucket {BucketName} with format version {FormatVersion}",
            uploadDetails.S3FileKey.FileExternalId,
            uploadDetails.Part.Number,
            bucketName,
            uploadDetails.EncryptionMode.FormatVersion);

        try
        {
            using var filePart = FileEncryption.PrepareFilePartForUpload(
                input: input,
                fileSizeInBytes: uploadDetails.FileSizeInBytes,
                filePart: uploadDetails.Part,
                encryptionMode: uploadDetails.EncryptionMode,
                cancellationToken: cancellationToken);

            var etag = await UploadToS3(
                partNumber: uploadDetails.Part.Number,
                s3FileKey: uploadDetails.S3FileKey,
                s3UploadId: uploadDetails.S3UploadId,
                bucketName: bucketName,
                fileBytes: filePart.Memory,
                uploadAlgorithm: uploadDetails.UploadAlgorithm,
                cancellationToken: cancellationToken);

            var duration = DateTime.UtcNow - startTime;

            Log.Debug(
                "Successfully uploaded part {PartNumber} of file {FileExternalId} bucket {BucketName} in {DurationMs}ms (ETag: {ETag})",
                uploadDetails.Part.Number,
                uploadDetails.S3FileKey.FileExternalId,
                bucketName,
                duration.TotalMilliseconds,
                etag);

            return new FilePartUploadResult(ETag: etag);
        }
        catch (OperationCanceledException)
        {
            Log.Warning(
                "Upload operation cancelled for file {FileExternalId} part {PartNumber} bucket {BucketName}",
                uploadDetails.S3FileKey.FileExternalId,
                uploadDetails.Part.Number,
                bucketName);

            throw;
        }
        catch (Exception e)
        {
            Log.Error(
                e,
                "Failed to upload part {PartNumber} of file {FileExternalId} bucket {BucketName}. Error: {ErrorMessage}",
                uploadDetails.Part.Number,
                uploadDetails.S3FileKey.FileExternalId,
                bucketName,
                e.Message);

            throw;
        }
    }

    public async ValueTask<FilePartUploadResult> UploadFilePart(
        PipeReader input,
        UploadFilePartDetails uploadDetails,
        string bucketName,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        Log.Debug(
            "Starting upload operation for file {FileExternalId} part {PartNumber} to bucket {BucketName} with format version {FormatVersion}",
            uploadDetails.S3FileKey.FileExternalId,
            uploadDetails.Part.Number,
            bucketName,
            uploadDetails.EncryptionMode.FormatVersion);

        try
        {
            using var filePart = await FileEncryption.PrepareFilePartForUpload(
                input: input,
                fileSizeInBytes: uploadDetails.FileSizeInBytes,
                filePart: uploadDetails.Part,
                encryptionMode: uploadDetails.EncryptionMode,
                cancellationToken: cancellationToken);

            var etag = await UploadToS3(
                partNumber: uploadDetails.Part.Number,
                s3FileKey: uploadDetails.S3FileKey,
                s3UploadId: uploadDetails.S3UploadId,
                bucketName: bucketName,
                fileBytes: filePart.Memory,
                uploadAlgorithm: uploadDetails.UploadAlgorithm,
                cancellationToken: cancellationToken);

            var duration = DateTime.UtcNow - startTime;

            Log.Debug(
                "Successfully uploaded part {PartNumber} of file {FileExternalId} bucket {BucketName} in {DurationMs}ms (ETag: {ETag})",
                uploadDetails.Part.Number,
                uploadDetails.S3FileKey.FileExternalId,
                bucketName,
                duration.TotalMilliseconds,
                etag);

            return new FilePartUploadResult(ETag: etag);
        }
        catch (OperationCanceledException)
        {
            Log.Warning(
                "Upload operation cancelled for file {FileExternalId} part {PartNumber} bucket {BucketName}",
                uploadDetails.S3FileKey.FileExternalId,
                uploadDetails.Part.Number,
                bucketName);

            throw;
        }
        catch (Exception e)
        {
            Log.Error(
                e,
                "Failed to upload part {PartNumber} of file {FileExternalId} bucket {BucketName}. Error: {ErrorMessage}",
                uploadDetails.Part.Number,
                uploadDetails.S3FileKey.FileExternalId,
                bucketName,
                e.Message);

            throw;
        }
    }

    private async Task<string> UploadToS3(
        int partNumber,
        S3FileKey s3FileKey,
        string? s3UploadId,
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
                    s3FileKey: s3FileKey,
                    bucketName: bucketName,
                    fileBytes: fileBytes,
                    cancellationToken: cancellationToken),

                UploadAlgorithm.MultiStepChunkUpload => await UploadPart(
                    partNumber: partNumber,
                    s3FileKey: s3FileKey,
                    s3UploadId: s3UploadId,
                    bucketName: bucketName,
                    fileBytes: fileBytes,
                    cancellationToken: cancellationToken),

                UploadAlgorithm.SingleChunkUpload => throw new NotSupportedException(
                    message:
                    $"Upload algorithm '{uploadAlgorithm}' is not supported for {nameof(S3StorageClient)}"),

                _ => throw new ArgumentOutOfRangeException(
                    paramName: nameof(uploadAlgorithm),
                    message: $"Upload algorithm '{uploadAlgorithm}' is not recognized")
            };
        }
        catch (Exception e)
        {
            if (uploadAlgorithm == UploadAlgorithm.MultiStepChunkUpload)
            {
                Log.Error(
                    e,
                    "S3 upload failed for file {FileExternalId} part {PartNumber}. Error: {ErrorMessage}",
                    s3FileKey.FileExternalId,
                    partNumber,
                    e.Message);
            }

            if (uploadAlgorithm == UploadAlgorithm.DirectUpload)
            {
                Log.Error(
                    e,
                    "S3 direct upload failed for file {FileExternalId}. Error: {ErrorMessage}",
                    s3FileKey.FileExternalId,
                    e.Message);
            }

            throw;
        }
    }

    private async Task<string> UploadPart(
        int partNumber,
        S3FileKey s3FileKey,
        string? s3UploadId,
        string bucketName,
        ReadOnlyMemory<byte> fileBytes,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        Log.Debug(
            "Initiating S3 upload for file {FileExternalId} part {PartNumber} (Size: {PartSize} bytes, UploadId: {UploadId})",
            s3FileKey.FileExternalId,
            partNumber,
            fileBytes.Length,
            s3UploadId);

        var etag = await ExecuteUploadPartRequest(
            fileBytes: fileBytes,
            bucketName: bucketName,
            key: s3FileKey,
            uploadId: s3UploadId,
            partNumber: partNumber,
            cancellationToken: cancellationToken);

        var duration = DateTime.UtcNow - startTime;

        Log.Debug(
            "Completed S3 upload for file {FileExternalId} part {PartNumber} in {DurationMs}ms (ETag: {ETag}, Speed: {SpeedMBps:F2} MB/s)",
            s3FileKey.FileExternalId,
            partNumber,
            duration.TotalMilliseconds,
            etag,
            fileBytes.Length / 1024.0 / 1024.0 / duration.TotalSeconds);

        return etag;
    }

    private async Task<string> UploadWholeFile(
        S3FileKey s3FileKey,
        string bucketName,
        ReadOnlyMemory<byte> fileBytes,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        Log.Debug(
            "Initiating S3 direct upload for file {FileExternalId} (Size: {FileSize} bytes)",
            s3FileKey.FileExternalId,
            fileBytes.Length);

        var etag = await ExecutePutObjectRequest(
            fileBytes: fileBytes,
            bucketName: bucketName,
            key: s3FileKey,
            cancellationToken: cancellationToken);

        var duration = DateTime.UtcNow - startTime;

        Log.Debug(
            "Completed S3 direct upload for file {FileExternalId} n {DurationMs}ms (ETag: {ETag}, Speed: {SpeedMBps:F2} MB/s)",
            s3FileKey.FileExternalId,
            duration.TotalMilliseconds,
            etag,
            fileBytes.Length / 1024.0 / 1024.0 / duration.TotalSeconds);

        return etag;
    }

    private async Task<string> ExecuteUploadPartRequest(
        ReadOnlyMemory<byte> fileBytes,
        string bucketName,
        S3FileKey key,
        string? uploadId,
        int partNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(uploadId))
            throw new InvalidOperationException(
                $"Cannot upload file part: S3UploadId is null for file " +
                $"'{key.FileExternalId}' part {partNumber}.");

        try
        {
            using var permission = await _rateLimiter.AcquirePermission(
                cancellationToken);

            var request = new UploadPartRequest
            {
                BucketName = bucketName,
                UploadId = uploadId,
                Key = key.Value,
                PartNumber = partNumber,
                InputStream = fileBytes.AsStream(),
                DisablePayloadSigning = true,
                DisableDefaultChecksumValidation = true,
                UseChunkEncoding = false,
                PartSize = fileBytes.Length,
            };

            var uploadPartResponse = await s3Client.UploadPartAsync(
                request: request,
                cancellationToken: cancellationToken);

            return uploadPartResponse.ETag;
        }
        catch (Exception e)
        {
            Log.Error(e, "[S3] Something went wrong while uploading file part '{BucketName}/{Key}/{PartNumber}'",
                bucketName,
                key,
                partNumber);

            throw;
        }
    }

    private async Task<string> ExecutePutObjectRequest(
        ReadOnlyMemory<byte> fileBytes,
        string bucketName,
        S3FileKey key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var permission = await _rateLimiter.AcquirePermission(
                cancellationToken);

            var request = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = key.Value,
                InputStream = fileBytes.AsStream(),
                DisablePayloadSigning = true,
                DisableDefaultChecksumValidation = true,
                UseChunkEncoding = false,
            };

            var response = await s3Client.PutObjectAsync(request, cancellationToken);
            return response.ETag;
        }
        catch (Exception e)
        {
            Log.Error(e, "[S3] Failed to upload file '{BucketName}/{Key}'",
                bucketName,
                key);

            throw;
        }
    }

    public void Dispose()
    {
        _rateLimiter.Dispose();
    }
}

public readonly record struct InitiatedUpload(
    string S3UploadId);