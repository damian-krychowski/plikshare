using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;

namespace PlikShare.Storages.S3.AwsS3.Create.Contracts;

public record CreateAwsS3StorageRequestDto(
    string Name,
    string AccessKey,
    string SecretAccessKey,
    string Region,
    StorageEncryptionType EncryptionType);

public record CreateAwsS3StorageResponseDto(
    StorageExtId ExternalId);