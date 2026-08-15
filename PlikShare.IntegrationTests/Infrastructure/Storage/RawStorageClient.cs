namespace PlikShare.IntegrationTests.Infrastructure.Storage;

/// <summary>
/// Factory for <see cref="IRawStorageClient"/> instances. Tests dispatch on the
/// storage's <see cref="TestStorageBackend"/> so the same assertion code (read at-rest
/// bytes / wait for physical deletion) works against any backend.
/// </summary>
public static class RawStorageClient
{
    public static IRawStorageClient For(
        TestFixture.AppStorage storage,
        TestFixture.AppVolume mainVolume)
    {
        return storage.Type switch
        {
            TestStorageBackend.HardDrive => new HardDriveRawStorageClient(
                storageRoot: $"{mainVolume.Path}/{storage.Name}"),

            TestStorageBackend.AzureBlob => new AzureBlobRawStorageClient(),

            TestStorageBackend.AwsS3
                or TestStorageBackend.CloudflareR2
                or TestStorageBackend.BackblazeB2
                or TestStorageBackend.DigitalOceanSpaces
                or TestStorageBackend.GoogleCloudStorage
                or TestStorageBackend.S3CompatibleMinio
                or TestStorageBackend.S3CompatibleRustfs => new S3RawStorageClient(storage.Type),

            _ => throw new ArgumentOutOfRangeException(
                nameof(storage), storage.Type, "Unsupported storage type for raw access.")
        };
    }
}
