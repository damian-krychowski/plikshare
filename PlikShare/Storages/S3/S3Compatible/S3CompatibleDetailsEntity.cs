namespace PlikShare.Storages.S3.S3Compatible;

public record S3CompatibleDetailsEntity(
    string AccessKey,
    string SecretKey,
    string Url,
    string? Region,
    bool ForcePathStyle);
