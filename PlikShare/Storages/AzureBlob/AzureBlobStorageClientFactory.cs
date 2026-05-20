using PlikShare.Core.Clock;
using PlikShare.Core.Configuration;
using PlikShare.Core.Utils;
using PlikShare.Storages.Entities;

namespace PlikShare.Storages.AzureBlob;

public class AzureBlobStorageClientFactory(
    IConfig config) : IStorageClientFactory<AzureBlobDetailsEntity>
{
    public async Task<StorageClientFactoryResult> Prepare(
        AzureBlobDetailsEntity input,
        CancellationToken cancellationToken)
    {
        var (code, client) = await AzureBlobClient.BuildAndTestConnection(
            details: input,
            cancellationToken: cancellationToken);

        if (code == AzureBlobClient.AzureBlobResultCode.InvalidUrl)
            return new StorageClientFactoryResult(Code: StorageOperationResultCode.InvalidUrl);

        if (code == AzureBlobClient.AzureBlobResultCode.CouldNotConnect)
            return new StorageClientFactoryResult(Code: StorageOperationResultCode.CouldNotConnect);

        if (client is null)
        {
            throw new InvalidOperationException(
                $"Azure Blob client was null despite successful connection test (Code: {code}). This should never happen.");
        }

        return new StorageClientFactoryResult(
            Code: StorageOperationResultCode.Ok,
            Details: new StoragePreparationDetails
            {
                StorageType = StorageType.AzureBlob,
                DetailsJson = Json.Serialize(input),

                StorageClientFactory = clientDetails => new AzureBlobStorageClient(
                    appUrl: config.AppUrl,
                    blobServiceClient: client,
                    storageId: clientDetails.StorageId,
                    externalId: clientDetails.ExternalId,
                    name: clientDetails.Name,
                    encryption: clientDetails.Encryption,
                    defaultTrashPolicy: clientDetails.DefaultTrashPolicy)
            });
    }
}
