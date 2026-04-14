using PlikShare.Core.Encryption;

namespace PlikShare.Storages.Encryption;

public class EncryptionKeyProvider
{
    public ManagedEncryptionKeyProvider? Managed { get; init; }
    public FullEncryptionKeyProvider? Full { get; init; }

    public byte GetLatestKeyVersion()
    {
        if (Managed is not null)
            return Managed.GetLatestKeyVersion();

        if (Full is not null)
            return Full.GetLatestKeyVersion();

        throw new InvalidOperationException(
            "Cannot determine latest key version because neither Managed nor Full encryption key provider is configured.");
    }
}

public static class StorageClientExtensions
{
    extension(IStorageClient storageClient)
    {
        public byte[] GetEncryptionKey(
            byte version,
            FullEncryptionSession? fullEncryptionSession)
        {
            if (storageClient.EncryptionType == StorageEncryptionType.None)
            {
                throw new InvalidOperationException(
                    $"Cannot get encryption key with version '{version}' for storage '{storageClient.ExternalId}' " +
                    $"because encryption type is '{StorageEncryptionType.None}'.");
            }

            if (storageClient.EncryptionType == StorageEncryptionType.Managed)
            {
                var keyProvider = storageClient.GetManagedEncryptionKeyProviderOrThrow();

                return keyProvider.GetEncryptionKey(
                    version);
            }

            if (storageClient.EncryptionType == StorageEncryptionType.Full)
            {
                if (fullEncryptionSession is null)
                {
                    throw new ArgumentNullException(
                        nameof(fullEncryptionSession),
                        $"Full encryption access is required for storage '{storageClient.ExternalId}' " +
                        $"with encryption type '{StorageEncryptionType.Full}'.");
                }

                var keyProvider = storageClient.GetFullEncryptionKeyProviderOrThrow();

                return keyProvider.GetEncryptionKey(
                    version,
                    fullEncryptionSession.Kek);
            }

            throw new InvalidOperationException(
                $"Unsupported encryption type '{storageClient.EncryptionType}' " +
                $"for storage '{storageClient.ExternalId}'.");
        }
        
        private ManagedEncryptionKeyProvider GetManagedEncryptionKeyProviderOrThrow()
        {
            if (storageClient.EncryptionType != StorageEncryptionType.Managed)
                throw new InvalidOperationException(
                    $"Cannot get managed encryption key provider for storage '{storageClient.ExternalId}' " +
                    $"because encryption type is '{storageClient.EncryptionType}', not '{StorageEncryptionType.Managed}'.");

            return storageClient
                .EncryptionKeyProvider
                ?.Managed ?? throw new InvalidOperationException(
                $"Managed encryption key provider is not configured " +
                $"for storage '{storageClient.ExternalId}' " +
                $"despite encryption type being set to '{StorageEncryptionType.Managed}'.");
        }

        private FullEncryptionKeyProvider GetFullEncryptionKeyProviderOrThrow()
        {
            if (storageClient.EncryptionType != StorageEncryptionType.Full)
                throw new InvalidOperationException(
                    $"Cannot get full encryption key provider for storage '{storageClient.ExternalId}' " +
                    $"because encryption type is '{storageClient.EncryptionType}', not '{StorageEncryptionType.Full}'.");

            return storageClient
                .EncryptionKeyProvider
                ?.Full ?? throw new InvalidOperationException(
                $"Full encryption key provider is not configured " +
                $"for storage '{storageClient.ExternalId}' " +
                $"despite encryption type being set to '{StorageEncryptionType.Full}'.");
        }
    }
}