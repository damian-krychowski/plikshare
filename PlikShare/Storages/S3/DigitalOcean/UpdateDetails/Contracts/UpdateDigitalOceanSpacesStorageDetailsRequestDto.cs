namespace PlikShare.Storages.S3.DigitalOcean.UpdateDetails.Contracts;

public record UpdateDigitalOceanSpacesStorageDetailsRequestDto(
    string AccessKey,
    string SecretKey,
    string Region);