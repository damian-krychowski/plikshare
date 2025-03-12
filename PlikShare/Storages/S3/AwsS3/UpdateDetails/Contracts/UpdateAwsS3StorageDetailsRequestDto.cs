namespace PlikShare.Storages.S3.AwsS3.UpdateDetails.Contracts;

public record UpdateAwsS3StorageDetailsRequestDto(
    string AccessKey,
    string SecretAccessKey,
    string Region);