using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;

namespace PlikShare.Storages.S3.DigitalOcean.Create.Contracts;

public record CreateDigitalOceanSpacesStorageRequestDto(
    string Name,
    string AccessKey,
    string SecretKey,
    string Region,
    StorageEncryptionType EncryptionType);

public record CreateDigitalOceanSpacesStorageResponseDto(
    StorageExtId ExternalId);