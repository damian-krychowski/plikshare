using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;
using PlikShare.Storages.List.Contracts;

namespace PlikShare.Storages.HardDrive.Create.Contracts;

public record CreateHardDriveStorageRequestDto(
    string Name,
    string VolumePath,
    string FolderPath,
    StorageEncryptionType EncryptionType,
    TrashPolicyDto DefaultTrashPolicy);

public record CreateHardDriveStorageResponseDto(
    StorageExtId ExternalId,
    string? RecoveryCode = null);