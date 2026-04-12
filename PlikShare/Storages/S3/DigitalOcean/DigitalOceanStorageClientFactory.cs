using PlikShare.Core.Clock;
using PlikShare.Core.Configuration;
using PlikShare.Core.Utils;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Storages.Entities;

namespace PlikShare.Storages.S3.DigitalOcean;

public class DigitalOceanStorageClientFactory(
    IConfig config,
    IClock clock,
    PreSignedUrlsService preSignedUrlsService) : IStorageClientFactory<DigitalOceanSpacesDetailsEntity>
{
    public async Task<StorageClientFactoryResult> Prepare(
        DigitalOceanSpacesDetailsEntity input,
        CancellationToken cancellationToken)
    {
        var s3ClientResult = await S3Client.BuildDigitalOceanSpacesAndTestConnection(
            accessKey: input.AccessKey,
            secretKey: input.SecretKey,
            url: input.Url,
            cancellationToken: cancellationToken);

        if (s3ClientResult.Code == S3Client.DigitalOceanSpacesResultCode.InvalidUrl)
            return new StorageClientFactoryResult(Code: StorageOperationResultCode.InvalidUrl);

        if (s3ClientResult.Code == S3Client.DigitalOceanSpacesResultCode.CouldNotConnect)
            return new StorageClientFactoryResult(Code: StorageOperationResultCode.CouldNotConnect);

        if (s3ClientResult.Client is null)
        {
            throw new InvalidOperationException(
                $"DigitalOcean Spaces S3 client was null despite successful connection test (Code: {s3ClientResult.Code}). This should never happen.");
        }

        return new StorageClientFactoryResult(
            Code: StorageOperationResultCode.Ok,
            Details: new StoragePreparationDetails
            {
                StorageType = StorageType.DigitalOceanSpaces,
                DetailsJson = Json.Serialize(input),
                StorageClientFactory = clientDetails => new S3StorageClient(
                    appUrl: config.AppUrl,
                    clock: clock,
                    s3Client: s3ClientResult.Client,
                    storageId: clientDetails.StorageId,
                    externalId: clientDetails.ExternalId,
                    storageType: StorageType.DigitalOceanSpaces,
                    preSignedUrlsService: preSignedUrlsService,
                    encryptionType: clientDetails.EncryptionType,
                    encryptionDetails: clientDetails.EncryptionDetails)
            });
    }
}
