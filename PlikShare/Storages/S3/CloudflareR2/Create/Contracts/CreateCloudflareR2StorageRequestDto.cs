using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;

namespace PlikShare.Storages.S3.CloudflareR2.Create.Contracts;

public record CreateCloudflareR2StorageRequestDto(
    string Name,
    string AccessKeyId,
    string SecretAccessKey,
    string Url,
    StorageEncryptionType EncryptionType);

public record CreateCloudflareR2StorageResponseDto(
    StorageExtId ExternalId);