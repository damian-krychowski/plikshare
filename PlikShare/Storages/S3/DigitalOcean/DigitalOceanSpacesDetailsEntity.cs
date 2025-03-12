namespace PlikShare.Storages.S3.DigitalOcean;

public record DigitalOceanSpacesDetailsEntity(
    string AccessKey,
    string SecretKey,
    string Url);