namespace PlikShare.Storages.S3.S3Compatible.UpdateDetails.Contracts;

public record UpdateS3CompatibleStorageDetailsRequestDto(
    string AccessKey,
    string SecretKey,
    string Url,
    string? Region,
    bool ForcePathStyle);
