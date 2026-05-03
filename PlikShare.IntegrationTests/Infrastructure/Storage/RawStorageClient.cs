using PlikShare.Storages.Entities;

namespace PlikShare.IntegrationTests.Infrastructure.Storage;

/// <summary>
/// Factory for <see cref="IRawStorageClient"/> instances. Tests dispatch on the
/// storage's <see cref="StorageType"/> so the same assertion code (read at-rest
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
            StorageType.HardDrive => new HardDriveRawStorageClient(
                storageRoot: $"{mainVolume.Path}/{storage.Name}"),

            StorageType.AzureBlob => new AzureBlobRawStorageClient(),

            StorageType.AwsS3
                or StorageType.CloudflareR2
                or StorageType.BackblazeB2
                or StorageType.DigitalOceanSpaces => new S3RawStorageClient(storage.Type),

            _ => throw new ArgumentOutOfRangeException(
                nameof(storage), storage.Type, "Unsupported storage type for raw access.")
        };
    }
}
