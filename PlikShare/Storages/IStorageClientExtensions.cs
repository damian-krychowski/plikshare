using PlikShare.Core.Encryption;
using PlikShare.Storages.Encryption;

namespace PlikShare.Storages;

public static class IStorageClientExtensions
{
    public static FileEncryption GenerateFileEncryptionDetails(
        this IStorageClient client)
    {
        return new FileEncryption
        {
            EncryptionType = client.EncryptionType,
            Metadata = GetFileEncryptionMetadata(client)
        };
    }
    private static FileEncryptionMetadata? GetFileEncryptionMetadata(IStorageClient client)
    {
        if (client.EncryptionType == StorageEncryptionType.None)
            return null;

        if (client.EncryptionType != StorageEncryptionType.Managed &&
            client.EncryptionType != StorageEncryptionType.Full)
            throw new InvalidOperationException(
                $"Unsupported encryption type '{client.EncryptionType}' " +
                $"for storage '{client.ExternalId}'.");

        return new FileEncryptionMetadata
        {
            KeyVersion = client
                .EncryptionKeyProvider
                !.GetLatestKeyVersion(),

            Salt = Aes256GcmStreaming.GenerateSalt(),
            NoncePrefix = Aes256GcmStreaming.GenerateNoncePrefix()
        };
    }
}