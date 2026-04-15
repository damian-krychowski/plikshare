using PlikShare.Storages.AzureBlob;
using PlikShare.Storages.Encryption;

namespace PlikShare.Storages.AzureBlob.UpdateDetails.Contracts;

public record UpdateAzureBlobStorageDetailsRequestDto(
    string AccountName,
    string AccountKey,
    string ServiceUrl,
    StorageEncryptionType EncryptionType,
    string AuthType = AzureBlobAuthType.SharedKey,
    string? SasToken = null,
    string? ManagedIdentityClientId = null);
