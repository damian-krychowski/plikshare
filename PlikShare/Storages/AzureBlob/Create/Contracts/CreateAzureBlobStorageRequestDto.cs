using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;
using PlikShare.Storages.List.Contracts;

namespace PlikShare.Storages.AzureBlob.Create.Contracts;

public record CreateAzureBlobStorageRequestDto(
    string Name,
    AzureBlobAuthType AuthType,
    string ServiceUrl,
    string? AccountName,
    string? AccountKey,
    string? SasToken,
    StorageEncryptionType EncryptionType,
    TrashPolicyDto DefaultTrashPolicy);

public record CreateAzureBlobStorageResponseDto(
    StorageExtId ExternalId,
    string? RecoveryCode = null);
