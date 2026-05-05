using PlikShare.Storages.Encryption;
using PlikShare.Storages.Entities;

namespace PlikShare.IntegrationTests.Infrastructure.Storage;

/// <summary>
/// Shared <c>[MemberData]</c> sources for live storage integration tests. Centralises
/// the provider list and the cross-product with encryption modes so adding a new
/// provider or encryption type in the future is a one-line change rather than
/// touching every theory.
/// </summary>
public static class StorageTheoryData
{
    private static readonly StorageType[] AllProviderValues =
    [
        StorageType.AwsS3,
        StorageType.CloudflareR2,
        StorageType.BackblazeB2,
        StorageType.DigitalOceanSpaces,
        StorageType.GoogleCloudStorage
    ];

    /// <summary>
    /// Every storage backend exercised by generic per-storage tests: S3-compatible
    /// providers, the local HardDrive, and Azure Blob. Azure cases are expected to
    /// fail until <see cref="PlikShare.Storages.AzureBlob.AzureBlobStorageClient"/>'s
    /// upload/download implementation lands (Stage 2) — failures there are the signal
    /// to start that work, not a reason to skip.
    /// </summary>
    private static readonly StorageType[] AllStorageValues =
    [
        StorageType.AwsS3,
        StorageType.CloudflareR2,
        StorageType.BackblazeB2,
        StorageType.DigitalOceanSpaces,
        StorageType.HardDrive,
        StorageType.AzureBlob,
        StorageType.GoogleCloudStorage
    ];

    private static readonly StorageEncryptionType[] AllEncryptionTypes =
    [
        StorageEncryptionType.None,
        StorageEncryptionType.Managed,
        StorageEncryptionType.Full
    ];

    public static IEnumerable<object[]> AllProviders =>
        AllProviderValues.Select(p => new object[] { p });

    public static IEnumerable<object[]> AllProvidersAndEncryptionTypes =>
        from provider in AllProviderValues
        from encryption in AllEncryptionTypes
        select new object[] { provider, encryption };

    /// <summary>
    /// Cross-product over all providers with <see cref="StorageEncryptionType.None"/>
    /// pinned. For tests that exercise code paths only reachable on unencrypted
    /// storage (e.g. <c>SingleChunkUpload</c> in <c>S3StorageClient.ResolveUploadAlgorithm</c>,
    /// which Managed and Full encryption skip).
    /// </summary>
    public static IEnumerable<object[]> AllProvidersWithNoEncryption =>
        AllProviderValues.Select(p => new object[] { p, StorageEncryptionType.None });

    /// <summary>
    /// Cross-product over all providers, excluding <see cref="StorageEncryptionType.Full"/>.
    /// Full encryption requires the user's session-bound key for decryption, so flows
    /// that target unauthenticated external access (box share links) cannot serve
    /// fully-encrypted files — those are restricted to None / Managed.
    /// </summary>
    public static IEnumerable<object[]> AllProvidersWithoutFullEncryption =>
        from provider in AllProviderValues
        from encryption in new[] { StorageEncryptionType.None, StorageEncryptionType.Managed }
        select new object[] { provider, encryption };

    public static IEnumerable<object[]> AllStorages =>
        AllStorageValues.Select(p => new object[] { p });

    public static IEnumerable<object[]> AllStoragesAndEncryptionTypes =>
        from provider in AllStorageValues
        from encryption in AllEncryptionTypes
        select new object[] { provider, encryption };

    public static IEnumerable<object[]> AllStoragesWithNoEncryption =>
        AllStorageValues.Select(p => new object[] { p, StorageEncryptionType.None });

    public static IEnumerable<object[]> AllStoragesWithoutFullEncryption =>
        from provider in AllStorageValues
        from encryption in new[] { StorageEncryptionType.None, StorageEncryptionType.Managed }
        select new object[] { provider, encryption };
}
