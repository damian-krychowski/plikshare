using PlikShare.Storages.Encryption;

namespace PlikShare.IntegrationTests.Infrastructure.S3;

/// <summary>
/// Shared <c>[MemberData]</c> sources for S3 live integration tests. Centralises the
/// provider list and the cross-product with encryption modes so adding a new provider
/// or encryption type in the future is a one-line change rather than touching every
/// theory.
/// </summary>
public static class S3TheoryData
{
    private static readonly S3StorageProvider[] AllProviderValues =
    [
        S3StorageProvider.AwsS3,
        S3StorageProvider.CloudflareR2,
        S3StorageProvider.BackblazeB2,
        S3StorageProvider.DigitalOceanSpaces
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
}
