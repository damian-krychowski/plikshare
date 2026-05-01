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
}
