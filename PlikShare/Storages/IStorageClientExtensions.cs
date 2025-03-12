using PlikShare.Core.Encryption;
using PlikShare.Storages.Encryption;

namespace PlikShare.Storages;

public static class IStorageClientExtensions
{
    public static FileEncryption GenerateFileEncryptionDetails(
        this IStorageClient client)
    {
        switch (client.EncryptionType)
        {
            case StorageEncryptionType.None:
                return new FileEncryption
                {
                    EncryptionType = StorageEncryptionType.None
                };

            case StorageEncryptionType.Managed:
            {
                var keyProvider = client.EncryptionKeyProvider;

                return new FileEncryption
                {
                    EncryptionType = StorageEncryptionType.Managed,
                    Metadata = new FileEncryptionMetadata
                    {
                        KeyVersion = keyProvider!.GetLatestKeyVersion(),
                        Salt = keyProvider.GetRandomSalt(),
                        NoncePrefix = keyProvider.GenerateRandomNoncePrefix()
                    }
                };
            }

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(client.EncryptionType),
                    $"Unknown EncryptionType value: '{client.EncryptionType}'");
        }
    }
}