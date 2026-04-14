using PlikShare.Core.Encryption;
using PlikShare.Storages.Encryption;
using System.Security.Cryptography;

namespace PlikShare.Storages;

public static class IStorageClientExtensions
{
    public static FileEncryptionMetadata? GenerateFileEncryptionMetadata(
        this IStorageClient client)
    {
        if (client.EncryptionType == StorageEncryptionType.None)
            return null;

        if (client.EncryptionType == StorageEncryptionType.Managed)
        {
            return new FileEncryptionMetadata
            {
                FormatVersion = 1,
                KeyVersion = client
                    .EncryptionKeyProvider
                    !.GetLatestKeyVersion(),
                Salt = Aes256GcmStreamingV1.GenerateSalt(),
                NoncePrefix = Aes256GcmStreamingV1.GenerateNoncePrefix(),
                ChainStepSalts = []
            };
        }

        if (client.EncryptionType == StorageEncryptionType.Full)
        {
            throw new NotImplementedException(
                $"Full encryption write path is not yet wired for storage '{client.ExternalId}'. " +
                $"Workspace salt threading into the key derivation chain is pending.");
        }

        throw new InvalidOperationException(
            $"Unsupported encryption type '{client.EncryptionType}' " +
            $"for storage '{client.ExternalId}'.");
    }
}