using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;

namespace PlikShare.Storages.HardDrive.Create.Contracts;

public record CreateHardDriveStorageRequestDto(
    string Name, 
    string VolumePath, 
    string FolderPath,
    StorageEncryptionType EncryptionType);

public record CreateHardDriveStorageResponseDto(
    StorageExtId ExternalId);