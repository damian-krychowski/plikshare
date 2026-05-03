using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;

namespace PlikShare.Storages.AzureBlob.Create.Contracts;

public record CreateAzureBlobStorageRequestDto(
    string Name,
    AzureBlobAuthType AuthType,
    string ServiceUrl,
    string? AccountName,
    string? AccountKey,
    string? SasToken,
    StorageEncryptionType EncryptionType);

public record CreateAzureBlobStorageResponseDto(
    StorageExtId ExternalId,
    string? RecoveryCode = null);
