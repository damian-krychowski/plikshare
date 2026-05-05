using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;

namespace PlikShare.Storages.S3.GoogleCloudStorage.Create.Contracts;

public record CreateGoogleCloudStorageRequestDto(
    string Name,
    string AccessKey,
    string SecretKey,
    StorageEncryptionType EncryptionType);

public record CreateGoogleCloudStorageResponseDto(
    StorageExtId ExternalId,
    string? RecoveryCode = null);
