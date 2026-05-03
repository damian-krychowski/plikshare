using System.IO.Pipelines;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using CommunityToolkit.HighPerformance;
using PlikShare.Core.Clock;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Files.PreSignedLinks.RangeRequests;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.FileReading;
using PlikShare.Storages.Id;
using PlikShare.Uploads.Algorithm;
using PlikShare.Uploads.Chunking;
using Serilog;

namespace PlikShare.Storages.AzureBlob;

/// <summary>
/// Azure Blob Storage client. Maps PlikShare's S3-flavoured contract onto Azure
/// concepts: containers ↔ buckets, block blobs ↔ S3 multipart objects, base64
/// block IDs ↔ S3 part ETags. There is no Azure-side "upload id" — block IDs are
/// the join key, so <see cref="InitiateMultiPartUpload"/> returns a synthetic GUID
/// that nothing on the Azure side actually consumes.
/// </summary>
public class AzureBlobStorageClient(
    string appUrl,
    BlobServiceClient blobServiceClient,
    int storageId,
    StorageExtId externalId,
    string name,
    StorageEncryption encryption) : IObjectStorageClient, IDisposable
{
    public const int MicroFileThreshold = 1 * SizeInBytes.Mb; // 1MB

    public int StorageId { get; } = storageId;
    public StorageExtId ExternalId { get; } = externalId;
    public string Name { get; } = name;
    public StorageEncryption Encryption { get; } = encryption;
    
    private readonly RateLimiter _rateLimiter = new(100, 80);

    public async ValueTask DeleteFile(
        string bucketName,
        S3FileKey key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var blob = blobServiceClient
                .GetBlobContainerClient(bucketName)
                .GetBlobClient(key.Value);

            await blob.DeleteIfExistsAsync(
                snapshotsOption: DeleteSnapshotsOption.IncludeSnapshots,
                cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            Log.Error(e, "[AZURE_BLOB] Something went wrong while deleting a file '{ContainerName}/{BlobName}'",
                bucketName, key.Value);

            throw;
        }
    }

    public async ValueTask DeleteFiles(
        string bucketName,
        S3FileKey[] keys,
        CancellationToken cancellationToken = default)
    {
        if (keys.Length == 0)
            return;

        var container = blobServiceClient.GetBlobContainerClient(bucketName);
        var batchClient = blobServiceClient.GetBlobBatchClient();

        // Azure caps a single batch at 256 sub-operations.
        foreach (var chunk in keys.Chunk(256))
        {
            var blobUris = chunk
                .Select(k => container.GetBlobClient(k.Value).Uri)
                .ToArray();

            try
            {
                await batchClient.DeleteBlobsAsync(
                    blobUris: blobUris,
                    snapshotsOption: DeleteSnapshotsOption.IncludeSnapshots,
                    cancellationToken: cancellationToken);
            }
            catch (AggregateException ex) when (ex.InnerExceptions.All(IsAlreadyGone))
            {
                // Mirror S3 DeleteObjects: missing keys are not errors.
            }
            catch (AggregateException ex)
            {
                var realFailures = ex.InnerExceptions
                    .Where(e => !IsAlreadyGone(e))
                    .ToList();

                Log.Error(ex,
                    "[AZURE_BLOB] Batch delete had {FailureCount} non-404 failures in '{ContainerName}'",
                    realFailures.Count, bucketName);

                throw new AggregateException(realFailures);
            }
        }

        static bool IsAlreadyGone(Exception e) =>
            e is RequestFailedException { Status: 404 };
    }

    public async Task CompleteMultiPartUpload(
        string bucketName,
        S3FileKey key,
        string uploadId,
        List<UploadedFilePart> partETags,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var blockBlob = blobServiceClient
                .GetBlobContainerClient(bucketName)
                .GetBlockBlobClient(key.Value);

            // Block IDs are deterministic from the part number (see ToBlockId), and
            // the same formula is used at staging time on both paths — server-side
            // StageBlock and the SAS URL we hand to the client (&blockid=...). So we
            // regenerate them here instead of trusting UploadedFilePart.ETag, which
            // on the direct-SAS path carries Azure's response ETag (a content hash),
            // not the block ID we staged with.
            var orderedBlockIds = partETags
                .OrderBy(p => p.PartNumber)
                .Select(p => ToBlockId(p.PartNumber))
                .ToList();

            await blockBlob.CommitBlockListAsync(
                base64BlockIds: orderedBlockIds,
                cancellationToken: cancellationToken);

            Log.Information("[AZURE_BLOB] Multi part upload '{ContainerName}/{BlobName}' was completed",
                bucketName, key.Value);
        }
        catch (Exception e)
        {
            Log.Error(e,
                "[AZURE_BLOB] Something went wrong while completing a multi part upload '{ContainerName}/{BlobName}'",
                bucketName, key.Value);

            throw;
        }
    }

    public ValueTask<PreSignedUploadFullFileLink> GetPreSignedUploadFullFileLink(
        string bucketName,
        S3FileKey key,
        string contentType,
        DateTimeOffset expiresAt)
    {
        var blob = blobServiceClient
            .GetBlobContainerClient(bucketName)
            .GetBlobClient(key.Value);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = bucketName,
            BlobName = key.Value,
            Resource = "b",
            ExpiresOn = expiresAt,
            ContentType = contentType,
        };

        sasBuilder.SetPermissions(BlobSasPermissions.Write | BlobSasPermissions.Create);

        // Azure "Put Blob" rejects requests that don't carry x-ms-blob-type — the
        // header isn't part of the SAS signature, so the caller must add it.
        var link = new PreSignedUploadFullFileLink(
            Url: blob.GenerateSasUri(sasBuilder).ToString(),
            RequiredHeaders: [new RequiredHeader { Name = "x-ms-blob-type", Value = "BlockBlob" }]);

        return ValueTask.FromResult(link);
    }
    
    public async Task AbortMultiPartUpload(
        string bucketName,
        S3FileKey key,
        string uploadId,
        List<string> partETags,
        CancellationToken cancellationToken = default)
    {
        // Azure auto-expires uncommitted blocks after 7 days, so technically a no-op
        // would be safe. We delete defensively to free the blob name immediately and
        // mirror S3's AbortMultipartUpload semantics (no committed blob remains).
        await DeleteFile(
            bucketName: bucketName,
            key: key,
            cancellationToken: cancellationToken);
    }

    public ValueTask<InitiatedUpload> InitiateMultiPartUpload(
        string bucketName,
        S3FileKey key,
        CancellationToken cancellationToken = default)
    {
        // Azure has no "initiate" call — block IDs serve as the join key.
        return ValueTask.FromResult(new InitiatedUpload(S3UploadId: string.Empty));
    }

    public async Task CreateBucketIfDoesntExist(
        string bucketName,
        CancellationToken cancellationToken = default)
    {
        var container = blobServiceClient.GetBlobContainerClient(bucketName);

        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        await PutCors(cancellationToken);
    }

    public async Task DeleteBucket(
        string bucketName,
        CancellationToken cancellationToken = default)
    {
        var container = blobServiceClient.GetBlobContainerClient(bucketName);

        try
        {
            await container.DeleteIfExistsAsync(cancellationToken: cancellationToken);

            Log.Information("[AZURE_BLOB] Container '{ContainerName}' was deleted.", bucketName);
        }
        catch (Exception e)
        {
            Log.Error(e, "[AZURE_BLOB] Something went wrong while deleting a container '{ContainerName}'",
                bucketName);
            throw;
        }
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

    public async ValueTask<IStorageFile> DownloadFile(
        DownloadFileDetails details,
        string bucketName,
        CancellationToken cancellationToken)
    {
        Log.Debug(
            "Requesting file stream from Azure Blob for {FileExternalId} at {Key}",
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
                .ForContext("BlobName", details.S3FileKey.Value));
    }

    public async ValueTask<IStorageFile> DownloadFileRange(
        DownloadFileRangeDetails details,
        string bucketName,
        CancellationToken cancellationToken)
    {
        Log.Debug(
            "Requesting ranged ({Range}) file stream from Azure Blob for {FileExternalId} at {Key}",
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
                .ForContext("BlobName", details.S3FileKey.Value));
    }

    public async ValueTask<FilePartUploadResult> UploadFilePart(
        Memory<byte> input,
        UploadFilePartDetails uploadDetails,
        string bucketName,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        Log.Debug(
            "Starting Azure upload for file {FileExternalId} part {PartNumber} to container {ContainerName} with format version {FormatVersion}",
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

            var etag = await UploadToAzure(
                partNumber: uploadDetails.Part.Number,
                key: uploadDetails.S3FileKey,
                bucketName: bucketName,
                fileBytes: filePart.Memory,
                uploadAlgorithm: uploadDetails.UploadAlgorithm,
                cancellationToken: cancellationToken);

            var duration = DateTime.UtcNow - startTime;

            Log.Debug(
                "Successfully uploaded part {PartNumber} of file {FileExternalId} container {ContainerName} in {DurationMs}ms (ETag: {ETag})",
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
                "Azure upload cancelled for file {FileExternalId} part {PartNumber} container {ContainerName}",
                uploadDetails.S3FileKey.FileExternalId,
                uploadDetails.Part.Number,
                bucketName);

            throw;
        }
        catch (Exception e)
        {
            Log.Error(
                e,
                "Failed Azure upload of part {PartNumber} of file {FileExternalId} container {ContainerName}. Error: {ErrorMessage}",
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
            "Starting Azure upload for file {FileExternalId} part {PartNumber} to container {ContainerName} with format version {FormatVersion}",
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

            var etag = await UploadToAzure(
                partNumber: uploadDetails.Part.Number,
                key: uploadDetails.S3FileKey,
                bucketName: bucketName,
                fileBytes: filePart.Memory,
                uploadAlgorithm: uploadDetails.UploadAlgorithm,
                cancellationToken: cancellationToken);

            var duration = DateTime.UtcNow - startTime;

            Log.Debug(
                "Successfully uploaded part {PartNumber} of file {FileExternalId} container {ContainerName} in {DurationMs}ms (ETag: {ETag})",
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
                "Azure upload cancelled for file {FileExternalId} part {PartNumber} container {ContainerName}",
                uploadDetails.S3FileKey.FileExternalId,
                uploadDetails.Part.Number,
                bucketName);

            throw;
        }
        catch (Exception e)
        {
            Log.Error(
                e,
                "Failed Azure upload of part {PartNumber} of file {FileExternalId} container {ContainerName}. Error: {ErrorMessage}",
                uploadDetails.Part.Number,
                uploadDetails.S3FileKey.FileExternalId,
                bucketName,
                e.Message);

            throw;
        }
    }

    private async Task<string> UploadToAzure(
        int partNumber,
        S3FileKey key,
        string bucketName,
        ReadOnlyMemory<byte> fileBytes,
        UploadAlgorithm uploadAlgorithm,
        CancellationToken cancellationToken)
    {
        return uploadAlgorithm switch
        {
            UploadAlgorithm.DirectUpload => await UploadWholeFile(
                key: key,
                bucketName: bucketName,
                fileBytes: fileBytes,
                cancellationToken: cancellationToken),

            UploadAlgorithm.MultiStepChunkUpload => await StageBlock(
                partNumber: partNumber,
                key: key,
                bucketName: bucketName,
                fileBytes: fileBytes,
                cancellationToken: cancellationToken),

            UploadAlgorithm.SingleChunkUpload => throw new NotSupportedException(
                $"Upload algorithm '{uploadAlgorithm}' is not supported for {nameof(AzureBlobStorageClient)} server-side; " +
                "the client uploads directly via the pre-signed full-file SAS URL."),

            _ => throw new ArgumentOutOfRangeException(
                paramName: nameof(uploadAlgorithm),
                message: $"Upload algorithm '{uploadAlgorithm}' is not recognized")
        };
    }

    private async Task<string> StageBlock(
        int partNumber,
        S3FileKey key,
        string bucketName,
        ReadOnlyMemory<byte> fileBytes,
        CancellationToken cancellationToken)
    {
        using var permission = await _rateLimiter.AcquirePermission(cancellationToken);

        var blockBlob = blobServiceClient
            .GetBlobContainerClient(bucketName)
            .GetBlockBlobClient(key.Value);

        var blockId = ToBlockId(partNumber);

        await blockBlob.StageBlockAsync(
            base64BlockId: blockId,
            content: fileBytes.AsStream(),
            cancellationToken: cancellationToken);

        // We return the block ID to satisfy the IStorageClient contract (which
        // expects an ETag-shaped string per part). CompleteMultiPartUpload doesn't
        // actually consume this value for Azure — block IDs are regenerated there
        // from the part number — so the round-trip is informational only.
        return blockId;
    }

    private async Task<string> UploadWholeFile(
        S3FileKey key,
        string bucketName,
        ReadOnlyMemory<byte> fileBytes,
        CancellationToken cancellationToken)
    {
        using var permission = await _rateLimiter.AcquirePermission(cancellationToken);

        var blobClient = blobServiceClient
            .GetBlobContainerClient(bucketName)
            .GetBlobClient(key.Value);

        var response = await blobClient.UploadAsync(
            content: fileBytes.AsStream(),
            overwrite: true,
            cancellationToken: cancellationToken);

        return response.Value.ETag.ToString();
    }

    private async Task<Stream> GetFile(
        string bucketName,
        S3FileKey key,
        CancellationToken cancellationToken)
    {
        var blobClient = blobServiceClient
            .GetBlobContainerClient(bucketName)
            .GetBlobClient(key.Value);

        var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return response.Value.Content;
    }

    private async Task<Stream> GetFileRange(
        string bucketName,
        S3FileKey key,
        BytesRange range,
        CancellationToken cancellationToken)
    {
        var blobClient = blobServiceClient
            .GetBlobContainerClient(bucketName)
            .GetBlobClient(key.Value);

        var response = await blobClient.DownloadStreamingAsync(
            options: new BlobDownloadOptions
            {
                Range = new HttpRange(
                    offset: range.Start,
                    length: range.End - range.Start + 1)
            },
            cancellationToken: cancellationToken);

        return response.Value.Content;
    }

    public ValueTask<string> GetPreSignedUploadFilePartLink(
        string bucketName,
        S3FileKey key,
        string uploadId,
        int partNumber,
        string contentType,
        DateTimeOffset expiresAt)
    {
        var blockBlob = blobServiceClient
            .GetBlobContainerClient(bucketName)
            .GetBlockBlobClient(key.Value);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = bucketName,
            BlobName = key.Value,
            Resource = "b",
            ExpiresOn = expiresAt,
            ContentType = contentType
        };

        sasBuilder.SetPermissions(BlobSasPermissions.Write | BlobSasPermissions.Create);

        // The base SAS is for the blob; we layer on the comp=block&blockid query
        // parameters so the client's PUT lands on the StageBlock REST endpoint
        // rather than overwriting the whole blob.
        var baseUrl = blockBlob.GenerateSasUri(sasBuilder).ToString();
        var blockId = Uri.EscapeDataString(ToBlockId(partNumber));

        return ValueTask.FromResult($"{baseUrl}&comp=block&blockid={blockId}");
    }
    
    public ValueTask<string> GetPreSignedDownloadFileLink(
        string bucketName, 
        S3FileKey key, 
        string contentType,
        ContentDispositionType contentDisposition, 
        string fileName,
        DateTimeOffset expiresAt)
    {
        var blob = blobServiceClient
            .GetBlobContainerClient(bucketName)
            .GetBlobClient(key.Value);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = bucketName,
            BlobName = key.Value,
            Resource = "b",
            ExpiresOn = expiresAt,
            ContentType = contentType,
            ContentDisposition = ContentDispositionHelper.CreateContentDisposition(
                fileName: fileName,
                disposition: contentDisposition)
        };

        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var result = blob.GenerateSasUri(sasBuilder).ToString();

        return ValueTask.FromResult(result);
    }

    private async Task PutCors(CancellationToken cancellationToken)
    {
        try
        {
            var appOrigin = new Uri(appUrl).GetLeftPart(UriPartial.Authority);

            var serviceProperties = await blobServiceClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            var corsRules = serviceProperties.Value.Cors;

            if (corsRules.Any(r => r.AllowedOrigins.Contains(appOrigin)))
            {
                return;
            }

            corsRules.Add(new BlobCorsRule
            {
                AllowedMethods = "GET,PUT",
                AllowedOrigins = appOrigin,
                AllowedHeaders = "*",
                ExposedHeaders = "ETag",
                MaxAgeInSeconds = 3600
            });

            var newProperties = serviceProperties.Value;
            newProperties.Cors = corsRules;

            await blobServiceClient.SetPropertiesAsync(
                properties: newProperties,
                cancellationToken: cancellationToken);

            Log.Information("[AZURE_BLOB] CORS rule for '{AppOrigin}' was added.", appOrigin);
        }
        catch (Exception e)
        {
            // CORS is account-wide (not container-scoped), so a missing permission
            // here doesn't block the bucket lifecycle — we surface a warning and
            // assume an operator has already configured CORS in the Azure portal.
            Log.Warning(e,
                "[AZURE_BLOB] Failed to apply CORS settings. Uploads from the browser may require manual CORS configuration in Azure portal.");
        }
    }

    private static string ToBlockId(int partNumber)
    {
        // Azure block IDs must be base64 strings, all the same length within a blob.
        // A fixed 8-digit padded part number makes the pre-encoded string 13 bytes
        // ("part-00000001"), which keeps every block ID in the same blob equally
        // long without us tracking it explicitly.
        var plain = $"part-{partNumber:D8}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plain));
    }

    public void Dispose()
    {
        _rateLimiter.Dispose();
    }
}
