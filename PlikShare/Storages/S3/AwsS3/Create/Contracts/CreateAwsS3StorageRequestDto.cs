using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;
using PlikShare.Storages.List.Contracts;

namespace PlikShare.Storages.S3.AwsS3.Create.Contracts;

public record CreateAwsS3StorageRequestDto(
    string Name,
    string AccessKey,
    string SecretAccessKey,
    string Region,
    StorageEncryptionType EncryptionType,
    TrashPolicyDto DefaultTrashPolicy);

public record CreateAwsS3StorageResponseDto(
    StorageExtId ExternalId,
    string? RecoveryCode = null);