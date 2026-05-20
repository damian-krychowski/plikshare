using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;
using PlikShare.Storages.List.Contracts;

namespace PlikShare.Storages.S3.DigitalOcean.Create.Contracts;

public record CreateDigitalOceanSpacesStorageRequestDto(
    string Name,
    string AccessKey,
    string SecretKey,
    string Region,
    StorageEncryptionType EncryptionType,
    TrashPolicyDto DefaultTrashPolicy);

public record CreateDigitalOceanSpacesStorageResponseDto(
    StorageExtId ExternalId,
    string? RecoveryCode = null);