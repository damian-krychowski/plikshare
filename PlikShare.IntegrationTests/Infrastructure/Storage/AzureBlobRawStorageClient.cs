using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using PlikShare.Files.Id;

namespace PlikShare.IntegrationTests.Infrastructure.Storage;

/// <summary>
/// <see cref="IRawStorageClient"/> for Azure Blob storage. Containers correspond to
/// S3 buckets, and PlikShare composes blob names the same way as S3 keys
/// (<c>{FileExternalId}_{secretPart}</c>), so this client probes by prefix to find
/// the actual blob name.
/// </summary>
public sealed class AzureBlobRawStorageClient : IRawStorageClient
{
    private readonly BlobServiceClient _serviceClient;

    public AzureBlobRawStorageClient()
    {
        var credentials = S3StorageConfig.AzureBlob;
        var credential = new StorageSharedKeyCredential(
            accountName: credentials.AccountName,
            accountKey: credentials.AccountKey);

        _serviceClient = new BlobServiceClient(
            serviceUri: new Uri(credentials.ServiceUrl),
            credential: credential);
    }

    public async Task<byte[]> ReadFileBytes(
        string bucketName,
        FileExtId fileExternalId,
        CancellationToken cancellationToken = default)
    {
        var containerClient = _serviceClient.GetBlobContainerClient(bucketName);
        var blobName = await FindBlobByPrefix(
            containerClient: containerClient,
            blobPrefix: $"{fileExternalId.Value}_",
            cancellationToken: cancellationToken);

        var blobClient = containerClient.GetBlobClient(blobName);

        using var ms = new MemoryStream();
        await blobClient.DownloadToAsync(ms, cancellationToken);
        return ms.ToArray();
    }

    public async Task WaitForFileGone(
        string bucketName,
        FileExtId fileExternalId,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var containerClient = _serviceClient.GetBlobContainerClient(bucketName);

        // Mirror S3RawStorageClient: capture the blob name while the object still
        // exists, then poll. If the queue job already ran, treat "no match" as
        // "already gone" and succeed.
        string blobName;
        try
        {
            blobName = await FindBlobByPrefix(
                containerClient: containerClient,
                blobPrefix: $"{fileExternalId.Value}_",
                cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return;
        }

        var blobClient = containerClient.GetBlobClient(blobName);
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            var exists = await blobClient.ExistsAsync(cancellationToken);

            if (!exists.Value)
                return;

            await Task.Delay(200, cancellationToken);
        }

        throw new InvalidOperationException(
            $"Azure blob '{bucketName}/{blobName}' was not deleted within {timeout}.");
    }

    private static async Task<string> FindBlobByPrefix(
        BlobContainerClient containerClient,
        string blobPrefix,
        CancellationToken cancellationToken)
    {
        var matches = new List<string>();

        await foreach (var blob in containerClient.GetBlobsAsync(
            traits: BlobTraits.None,
            states: BlobStates.None,
            prefix: blobPrefix,
            cancellationToken: cancellationToken))
        {
            matches.Add(blob.Name);

            if (matches.Count > 1)
                break;
        }

        if (matches.Count == 0)
            throw new InvalidOperationException(
                $"No blob in container '{containerClient.Name}' matches prefix '{blobPrefix}'.");

        if (matches.Count > 1)
            throw new InvalidOperationException(
                $"Expected exactly one blob in '{containerClient.Name}' with prefix '{blobPrefix}', found more than one.");

        return matches[0];
    }

    public void Dispose()
    {
    }
}
