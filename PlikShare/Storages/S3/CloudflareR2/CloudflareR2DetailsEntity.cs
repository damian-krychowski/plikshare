namespace PlikShare.Storages.S3.CloudflareR2;

public record CloudflareR2DetailsEntity(
    string AccessKeyId,
    string SecretAccessKey,
    string Url);