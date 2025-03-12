namespace PlikShare.Storages.S3.CloudflareR2.UpdateDetails.Contracts;

public record UpdateCloudflareR2StorageDetailsRequestDto(
    string AccessKeyId,
    string SecretAccessKey,
    string Url);