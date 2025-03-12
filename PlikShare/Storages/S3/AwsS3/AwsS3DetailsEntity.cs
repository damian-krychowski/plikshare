namespace PlikShare.Storages.S3.AwsS3;

public record AwsS3DetailsEntity(
    string AccessKey,
    string SecretAccessKey,
    string Region);