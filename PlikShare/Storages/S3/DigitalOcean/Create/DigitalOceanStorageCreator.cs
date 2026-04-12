using PlikShare.Core.Clock;
using PlikShare.Core.Configuration;
using PlikShare.Core.Utils;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Storages.Create;
using PlikShare.Storages.Entities;

namespace PlikShare.Storages.S3.DigitalOcean.Create;

public class DigitalOceanStorageCreator(
    IConfig config,
    IClock clock,
    PreSignedUrlsService preSignedUrlsService) : IStorageCreator<DigitalOceanSpacesDetailsEntity>
{
    public async Task<StoragePreparation> Prepare(
        DigitalOceanSpacesDetailsEntity input,
        CancellationToken cancellationToken)
    {
        var s3ClientResult = await S3Client.BuildDigitalOceanSpacesAndTestConnection(
            accessKey: input.AccessKey,
            secretKey: input.SecretKey,
            url: input.Url,
            cancellationToken: cancellationToken);

        if (s3ClientResult.Code == S3Client.DigitalOceanSpacesResultCode.InvalidUrl)
            return new StoragePreparation(Code: StorageCreationResultCode.InvalidUrl);

        if (s3ClientResult.Code == S3Client.DigitalOceanSpacesResultCode.CouldNotConnect)
            return new StoragePreparation(Code: StorageCreationResultCode.CouldNotConnect);

        if (s3ClientResult.Client is null)
        {
            throw new InvalidOperationException(
                $"DigitalOcean Spaces S3 client was null despite successful connection test (Code: {s3ClientResult.Code}). This should never happen.");
        }

        return new StoragePreparation(
            Code: StorageCreationResultCode.Ok,
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
