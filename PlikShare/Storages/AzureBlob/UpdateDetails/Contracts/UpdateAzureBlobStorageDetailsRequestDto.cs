namespace PlikShare.Storages.AzureBlob.UpdateDetails.Contracts;

public record UpdateAzureBlobStorageDetailsRequestDto(
    AzureBlobAuthType AuthType,
    string ServiceUrl,
    string? AccountName,
    string? AccountKey,
    string? SasToken);
