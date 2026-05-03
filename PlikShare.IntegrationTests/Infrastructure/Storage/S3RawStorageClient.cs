using Amazon.S3;
using PlikShare.Files.Id;
using PlikShare.Storages.Entities;

namespace PlikShare.IntegrationTests.Infrastructure.Storage;

/// <summary>
/// <see cref="IRawStorageClient"/> adapter on top of <see cref="S3RawClient"/> for the
/// four S3-compatible providers (AWS, R2, B2, DigitalOcean). PlikShare composes the
/// S3 key as <c>{FileExternalId}_{secretPart}</c>, so this client probes by prefix to
/// discover the actual object key.
/// </summary>
public sealed class S3RawStorageClient : IRawStorageClient
{
    private readonly IAmazonS3 _client;

    public S3RawStorageClient(StorageType provider)
    {
        _client = S3RawClient.Build(provider);
    }

    public async Task<byte[]> ReadFileBytes(
        string bucketName,
        FileExtId fileExternalId,
        CancellationToken cancellationToken = default)
    {
        var key = await S3RawClient.FindKeyByPrefix(
            client: _client,
            bucketName: bucketName,
            keyPrefix: $"{fileExternalId.Value}_",
            cancellationToken: cancellationToken);

        return await S3RawClient.ReadObjectBytes(
            client: _client,
            bucketName: bucketName,
            key: key,
            cancellationToken: cancellationToken);
    }

    public async Task WaitForFileGone(
        string bucketName,
        FileExtId fileExternalId,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        // The bulk-delete S3 queue job may already have run before we get here — in
        // that case ListObjectsV2 against the prefix returns empty and there is no
        // key to wait on. Treat "no match" as "already gone" and succeed.
        string key;
        try
        {
            key = await S3RawClient.FindKeyByPrefix(
                client: _client,
                bucketName: bucketName,
                keyPrefix: $"{fileExternalId.Value}_",
                cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return;
        }

        await S3RawClient.WaitForObjectGone(
            client: _client,
            bucketName: bucketName,
            key: key,
            timeout: timeout,
            cancellationToken: cancellationToken);
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
