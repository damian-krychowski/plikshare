namespace PlikShare.EmailProviders.ExternalProviders.AwsSes;

public record AwsSesDetailsEntity(
    string AccessKey,
    string SecretAccessKey,
    string Region);