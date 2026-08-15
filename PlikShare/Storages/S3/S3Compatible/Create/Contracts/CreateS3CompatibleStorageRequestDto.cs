using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;
using PlikShare.Storages.List.Contracts;

namespace PlikShare.Storages.S3.S3Compatible.Create.Contracts;

public record CreateS3CompatibleStorageRequestDto(
    string Name,
    string AccessKey,
    string SecretKey,
    string Url,
    string? Region,
    bool ForcePathStyle,
    StorageEncryptionType EncryptionType,
    TrashPolicyDto DefaultTrashPolicy);

public record CreateS3CompatibleStorageResponseDto(
    StorageExtId ExternalId,
    string? RecoveryCode = null);
