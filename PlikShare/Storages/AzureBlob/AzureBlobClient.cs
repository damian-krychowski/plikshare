using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Serilog;

namespace PlikShare.Storages.AzureBlob;

public static class AzureBlobClient
{
    public static BlobServiceClient BuildClientOrThrow(
        AzureBlobDetailsEntity details)
    {
        var serviceUri = new Uri(details.ServiceUrl);

        return details.AuthType switch
        {
            AzureBlobAuthType.SharedKey => BuildSharedKeyClient(serviceUri, details),
            AzureBlobAuthType.Sas => BuildSasClient(serviceUri, details),

            _ => throw new ArgumentOutOfRangeException(
                paramName: nameof(details.AuthType),
                actualValue: details.AuthType,
                message: $"Unsupported Azure Blob auth type '{details.AuthType}'.")
        };
    }

    private static BlobServiceClient BuildSharedKeyClient(
        Uri serviceUri,
        AzureBlobDetailsEntity details)
    {
        if (string.IsNullOrWhiteSpace(details.AccountName) || string.IsNullOrWhiteSpace(details.AccountKey))
            throw new ArgumentException(
                "Account Name and Account Key are required for Azure Blob auth type 'shared-key'.");

        var credential = new StorageSharedKeyCredential(
            accountName: details.AccountName,
            accountKey: details.AccountKey);

        return new BlobServiceClient(
            serviceUri: serviceUri,
            credential: credential);
    }

    private static BlobServiceClient BuildSasClient(
        Uri serviceUri,
        AzureBlobDetailsEntity details)
    {
        if (string.IsNullOrWhiteSpace(details.SasToken))
            throw new ArgumentException(
                "SAS token is required for Azure Blob auth type 'sas'.");

        // Azure portal copies SAS tokens with a leading '?'; AzureSasCredential rejects it.
        var sasToken = details.SasToken.TrimStart('?');

        return new BlobServiceClient(
            serviceUri: serviceUri,
            credential: new AzureSasCredential(sasToken));
    }

    public static async Task<AzureBlobResult> BuildAndTestConnection(
        AzureBlobDetailsEntity details,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = BuildClientOrThrow(
                details: details);

            var isConnectionOk = await TestConnection(
                client: client,
                cancellationToken: cancellationToken);

            if (!isConnectionOk)
                return new AzureBlobResult(
                    Code: AzureBlobResultCode.CouldNotConnect);

            return new AzureBlobResult(
                Code: AzureBlobResultCode.Ok,
                Client: client);
        }
        catch (UriFormatException)
        {
            return new AzureBlobResult(
                Code: AzureBlobResultCode.InvalidUrl);
        }
        catch (ArgumentException e)
        {
            // Missing required fields per auth type — surfaces as a connection failure
            // to keep the API contract simple (the UI already validates required fields).
            Log.Warning(e, "[AZURE_BLOB] Could not build client for '{ServiceUrl}'", details.ServiceUrl);
            return new AzureBlobResult(
                Code: AzureBlobResultCode.CouldNotConnect);
        }
        catch (Exception e)
        {
            Log.Warning(e, "[AZURE_BLOB] Could not connect to Azure Blob storage at '{ServiceUrl}'", details.ServiceUrl);
            return new AzureBlobResult(
                Code: AzureBlobResultCode.CouldNotConnect);
        }
    }

    private static async Task<bool> TestConnection(
        BlobServiceClient client,
        CancellationToken cancellationToken = default)
    {
        // Mirrors the S3 probe: create+delete a throwaway container to prove that
        // the credentials carry both write and management permissions, not just
        // an account-level read.
        var randomContainerName = $"plikshare-probe-{Guid.NewGuid():N}";

        try
        {
            var containerClient = client.GetBlobContainerClient(randomContainerName);

            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            await containerClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);

            return true;
        }
        catch (Exception e)
        {
            Log.Warning(e, "[AZURE_BLOB] Connection test failed for service URL '{ServiceUrl}'",
                client.Uri);

            return false;
        }
    }

    public enum AzureBlobResultCode
    {
        Ok,
        InvalidUrl,
        CouldNotConnect
    }

    public readonly record struct AzureBlobResult(
        AzureBlobResultCode Code,
        BlobServiceClient? Client = null);
}
