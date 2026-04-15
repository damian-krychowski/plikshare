namespace PlikShare.Storages.AzureBlob;

public static class AzureBlobAuthType
{
    public const string SharedKey = "shared-key";
    public const string Sas = "sas";
    public const string EntraId = "entra-id";
}

public record AzureBlobDetailsEntity(
    string AccountName,
    string AccountKey,
    string ServiceUrl,
    string AuthType = AzureBlobAuthType.SharedKey,
    string? SasToken = null,
    string? ManagedIdentityClientId = null);
