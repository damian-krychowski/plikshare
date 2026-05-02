using Amazon.S3;
using Amazon.S3.Model;
using Serilog;

namespace PlikShare.IntegrationTests.Infrastructure.S3;

/// <summary>
/// Test-only fallback for tearing down S3-backed buckets when the production
/// <c>delete-bucket</c> queue job is too slow (mainly Backblaze B2's eventual
/// consistency on versioned buckets). We don't want this aggressive purge in
/// production code — it would delete object versions/markers that PlikShare
/// didn't put there. In tests we own the bucket end-to-end, so it's safe.
/// </summary>
public static class S3HardPurge
{
    public static async Task PurgeAndDeleteBucket(
        S3StorageProvider provider,
        string bucketName,
        CancellationToken cancellationToken = default)
    {
        var client = S3RawClient.Build(provider);

        try
        {
            await PurgeAllObjectVersions(client, bucketName, cancellationToken);
            await AbortAllMultipartUploads(client, bucketName, cancellationToken);

            await client.DeleteBucketAsync(
                bucketName: bucketName,
                cancellationToken: cancellationToken);

            Log.Information("[S3HardPurge] Bucket '{BucketName}' on {Provider} purged and deleted.",
                bucketName, provider);
        }
        catch (AmazonS3Exception e) when (e.ErrorCode == "NoSuchBucket")
        {
            Log.Information("[S3HardPurge] Bucket '{BucketName}' on {Provider} already gone — skipping.",
                bucketName, provider);
        }
        finally
        {
            client.Dispose();
        }
    }

    private static async Task PurgeAllObjectVersions(
        IAmazonS3 client,
        string bucketName,
        CancellationToken cancellationToken)
    {
        const int batchLimit = 1000;
        string? keyMarker = null;
        string? versionIdMarker = null;
        var totalDeleted = 0;

        while (true)
        {
            var listResponse = await client.ListVersionsAsync(new ListVersionsRequest
            {
                BucketName = bucketName,
                KeyMarker = keyMarker,
                VersionIdMarker = versionIdMarker,
                MaxKeys = batchLimit
            }, cancellationToken);

            var batch = new List<KeyVersion>(capacity: batchLimit);

            if (listResponse.Versions != null)
            {
                foreach (var version in listResponse.Versions)
                {
                    batch.Add(new KeyVersion
                    {
                        Key = version.Key,
                        VersionId = version.VersionId
                    });
                }
            }

            if (batch.Count > 0)
            {
                var deleteResponse = await client.DeleteObjectsAsync(new DeleteObjectsRequest
                {
                    BucketName = bucketName,
                    Objects = batch,
                    Quiet = true
                }, cancellationToken);

                if (deleteResponse.DeleteErrors != null && deleteResponse.DeleteErrors.Count > 0)
                {
                    foreach (var error in deleteResponse.DeleteErrors)
                    {
                        Log.Error(
                            "[S3HardPurge] Failed to purge '{BucketName}/{Key}' (versionId: {VersionId}): {Code} - {Message}",
                            bucketName, error.Key, error.VersionId, error.Code, error.Message);
                    }

                    throw new InvalidOperationException(
                        $"Failed to purge {deleteResponse.DeleteErrors.Count} object version(s) from bucket '{bucketName}'. See logs for details.");
                }

                totalDeleted += batch.Count;
            }

            if (listResponse.IsTruncated == true)
            {
                keyMarker = listResponse.NextKeyMarker;
                versionIdMarker = listResponse.NextVersionIdMarker;
            }
            else
            {
                break;
            }
        }

        if (totalDeleted > 0)
        {
            Log.Information("[S3HardPurge] Purged {Count} object version(s) from bucket '{BucketName}'.",
                totalDeleted, bucketName);
        }
    }

    private static async Task AbortAllMultipartUploads(
        IAmazonS3 client,
        string bucketName,
        CancellationToken cancellationToken)
    {
        string? keyMarker = null;
        string? uploadIdMarker = null;

        while (true)
        {
            var response = await client.ListMultipartUploadsAsync(new ListMultipartUploadsRequest
            {
                BucketName = bucketName,
                KeyMarker = keyMarker,
                UploadIdMarker = uploadIdMarker
            }, cancellationToken);

            if (response.MultipartUploads != null)
            {
                foreach (var upload in response.MultipartUploads)
                {
                    await client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest
                    {
                        BucketName = bucketName,
                        Key = upload.Key,
                        UploadId = upload.UploadId
                    }, cancellationToken);
                }
            }

            if (response.IsTruncated == true)
            {
                keyMarker = response.NextKeyMarker;
                uploadIdMarker = response.NextUploadIdMarker;
            }
            else
            {
                break;
            }
        }
    }
}
