namespace PlikShare.Storages.S3.GoogleCloudStorage.UpdateDetails.Contracts;

public record UpdateGoogleCloudStorageDetailsRequestDto(
    string AccessKey,
    string SecretKey);
