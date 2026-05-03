using PlikShare.Files.Id;

namespace PlikShare.IntegrationTests.Infrastructure.Storage;

/// <summary>
/// <see cref="IRawStorageClient"/> for the local hard-drive backend. Files live at
/// <c>{storageRoot}/{bucketName}/{fileExternalId}</c> — there is no secret-part suffix
/// because <see cref="PlikShare.Storages.HardDrive.StorageClient.HardDriveStorageClient.GenerateFileKeySecretPart"/>
/// returns an empty string for hard-drive storages.
/// </summary>
public sealed class HardDriveRawStorageClient : IRawStorageClient
{
    private readonly string _storageRoot;

    public HardDriveRawStorageClient(string storageRoot)
    {
        _storageRoot = storageRoot;
    }

    public Task<byte[]> ReadFileBytes(
        string bucketName,
        FileExtId fileExternalId,
        CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(_storageRoot, bucketName, fileExternalId.Value);

        if (!File.Exists(filePath))
            throw new InvalidOperationException(
                $"Hard-drive file '{filePath}' does not exist.");

        return File.ReadAllBytesAsync(filePath, cancellationToken);
    }

    public async Task WaitForFileGone(
        string bucketName,
        FileExtId fileExternalId,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(_storageRoot, bucketName, fileExternalId.Value);
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (!File.Exists(filePath))
                return;

            await Task.Delay(100, cancellationToken);
        }

        throw new InvalidOperationException(
            $"Hard-drive file '{filePath}' was not deleted within {timeout}.");
    }

    public void Dispose()
    {
    }
}
