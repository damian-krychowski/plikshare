using PlikShare.Storages.Entities;

namespace PlikShare.IntegrationTests.Infrastructure.Storage;

/// <summary>
/// Test-only storage dimension. Mirrors the production <see cref="StorageType"/> but
/// splits <see cref="StorageType.S3Compatible"/> into the concrete self-hosted backends
/// exercised by the live tests (MinIO, RustFS), so the whole matrix runs against each
/// of them while production keeps a single generic type.
/// </summary>
public enum TestStorageBackend
{
    HardDrive,
    AwsS3,
    CloudflareR2,
    DigitalOceanSpaces,
    BackblazeB2,
    AzureBlob,
    GoogleCloudStorage,
    S3CompatibleMinio,
    S3CompatibleRustfs
}
