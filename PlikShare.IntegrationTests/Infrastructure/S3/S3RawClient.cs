using Amazon.S3;
using Amazon.S3.Model;
using PlikShare.Storages.S3;

namespace PlikShare.IntegrationTests.Infrastructure.S3;

/// <summary>
/// Test-only helper for reading objects from S3 buckets directly (bypassing PlikShare's
/// API). Used to verify properties of the raw at-rest bytes — e.g. that a file uploaded
/// with Full encryption does not contain plaintext markers.
/// </summary>
public static class S3RawClient
{
    public static IAmazonS3 Build(S3StorageProvider provider)
    {
        return provider switch
        {
            S3StorageProvider.AwsS3 => S3Client.BuildAwsClientOrThrow(
                accessKey: S3StorageConfig.AwsS3.AccessKey,
                secretAccessKey: S3StorageConfig.AwsS3.SecretAccessKey,
                region: S3StorageConfig.AwsS3.Region),

            S3StorageProvider.CloudflareR2 => S3Client.BuildCloudflareClientOrThrow(
                accessKeyId: S3StorageConfig.CloudflareR2.AccessKeyId,
                secretAccessKey: S3StorageConfig.CloudflareR2.SecretAccessKey,
                url: S3StorageConfig.CloudflareR2.Url),

            S3StorageProvider.BackblazeB2 => S3Client.BuildBackblazeClientOrThrow(
                keyId: S3StorageConfig.BackblazeB2.KeyId,
                applicationKey: S3StorageConfig.BackblazeB2.ApplicationKey,
                url: S3StorageConfig.BackblazeB2.Url),

            S3StorageProvider.DigitalOceanSpaces => S3Client.BuildDigitalOceanSpacesClientOrThrow(
                accessKey: S3StorageConfig.DigitalOceanSpaces.AccessKey,
                secretKey: S3StorageConfig.DigitalOceanSpaces.SecretKey,
                url: $"https://{S3StorageConfig.DigitalOceanSpaces.Region}.digitaloceanspaces.com"),

            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
        };
    }

    /// <summary>
    /// Lists bucket objects under the given prefix and returns the single match.
    /// PlikShare composes the S3 key as <c>{FileExternalId}_{secretPart}</c>, so passing
    /// the file external id with a trailing underscore unambiguously points at one object.
    /// </summary>
    public static async Task<string> FindKeyByPrefix(
        IAmazonS3 client,
        string bucketName,
        string keyPrefix,
        CancellationToken cancellationToken = default)
    {
        var response = await client.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = bucketName,
            Prefix = keyPrefix
        }, cancellationToken);

        var objects = response.S3Objects ?? [];

        if (objects.Count == 0)
            throw new InvalidOperationException(
                $"No object in bucket '{bucketName}' matches prefix '{keyPrefix}'.");

        if (objects.Count > 1)
            throw new InvalidOperationException(
                $"Expected exactly one object in '{bucketName}' with prefix '{keyPrefix}', found {objects.Count}.");

        return objects[0].Key;
    }

    public static async Task<byte[]> ReadObjectBytes(
        IAmazonS3 client,
        string bucketName,
        string key,
        CancellationToken cancellationToken = default)
    {
        using var response = await client.GetObjectAsync(
            bucketName: bucketName,
            key: key,
            cancellationToken: cancellationToken);

        using var ms = new MemoryStream();
        await response.ResponseStream.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
    }

    /// <summary>
    /// Polls <c>GetObject</c> until the bucket no longer serves the key, or the timeout
    /// elapses. The bulk-delete S3 queue job runs asynchronously after the API call
    /// returns, so this captures the moment the file is genuinely unreachable on the
    /// underlying storage — not just hidden by the application DB. On versioned buckets
    /// (B2) <c>DeleteObject</c> places a delete marker; <c>GetObject</c> against the
    /// current version returns 404, so this assertion holds without needing per-provider
    /// branching.
    /// </summary>
    public static async Task WaitForObjectGone(
        IAmazonS3 client,
        string bucketName,
        string key,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var _ = await client.GetObjectAsync(
                    bucketName: bucketName,
                    key: key,
                    cancellationToken: cancellationToken);
            }
            catch (AmazonS3Exception e) when (
                e.StatusCode == System.Net.HttpStatusCode.NotFound
                || e.ErrorCode == "NoSuchKey")
            {
                return;
            }

            await Task.Delay(200, cancellationToken);
        }

        throw new InvalidOperationException(
            $"Object '{bucketName}/{key}' was not deleted within {timeout}.");
    }
}
