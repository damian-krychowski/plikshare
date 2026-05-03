namespace PlikShare.Storages.AzureBlob;

public record AzureBlobDetailsEntity(
    AzureBlobAuthType AuthType,
    string ServiceUrl,
    string? AccountName,
    string? AccountKey,
    string? SasToken);
