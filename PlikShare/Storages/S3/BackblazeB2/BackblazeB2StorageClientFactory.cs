using PlikShare.Core.Clock;
using PlikShare.Core.Configuration;
using PlikShare.Core.Utils;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Storages.Entities;

namespace PlikShare.Storages.S3.BackblazeB2;

public class BackblazeB2StorageClientFactory(
    IConfig config,
    IClock clock,
    PreSignedUrlsService preSignedUrlsService) : IStorageClientFactory<BackblazeB2DetailsEntity>
{
    public async Task<StorageClientFactoryResult> Prepare(
        BackblazeB2DetailsEntity input,
        CancellationToken cancellationToken)
    {
        var s3ClientResult = await S3Client.BuildBackblazeAndTestConnection(
            keyId: input.KeyId,
            applicationKey: input.ApplicationKey,
            url: input.Url,
            cancellationToken: cancellationToken);

        if (s3ClientResult.Code == S3Client.BackblazeResultCode.InvalidUrl)
            return new StorageClientFactoryResult(Code: StorageOperationResultCode.InvalidUrl);

        if (s3ClientResult.Code == S3Client.BackblazeResultCode.CouldNotConnect)
            return new StorageClientFactoryResult(Code: StorageOperationResultCode.CouldNotConnect);

        if (s3ClientResult.Client is null)
        {
            throw new InvalidOperationException(
                $"Backblaze B2 S3 client was null despite successful connection test (Code: {s3ClientResult.Code}). This should never happen.");
        }

        return new StorageClientFactoryResult(
            Code: StorageOperationResultCode.Ok,
            Details: new StoragePreparationDetails
            {
                StorageType = StorageType.BackblazeB2,
                DetailsJson = Json.Serialize(input),
                StorageClientFactory = clientDetails => new S3StorageClient(
                    appUrl: config.AppUrl,
                    clock: clock,
                    s3Client: s3ClientResult.Client,
                    storageId: clientDetails.StorageId,
                    externalId: clientDetails.ExternalId,
                    storageType: StorageType.BackblazeB2,
                    preSignedUrlsService: preSignedUrlsService,
                    encryptionType: clientDetails.EncryptionType,
                    encryptionDetails: clientDetails.EncryptionDetails)
            });
    }
}
