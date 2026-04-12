using PlikShare.Core.Clock;
using PlikShare.Core.Configuration;
using PlikShare.Core.Utils;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Storages.Entities;

namespace PlikShare.Storages.S3.AwsS3;

public class AwsS3StorageClientFactory(
    IConfig config,
    IClock clock,
    PreSignedUrlsService preSignedUrlsService) : IStorageClientFactory<AwsS3DetailsEntity>
{
    public async Task<StorageClientFactoryResult> Prepare(
        AwsS3DetailsEntity input,
        CancellationToken cancellationToken)
    {
        var s3ClientResult = await S3Client.BuildAwsAndTestConnection(
            accessKey: input.AccessKey,
            secretAccessKey: input.SecretAccessKey,
            region: input.Region,
            cancellationToken: cancellationToken);

        if (s3ClientResult.Code == S3Client.AwsResultCode.CouldNotConnect)
            return new StorageClientFactoryResult(Code: StorageOperationResultCode.CouldNotConnect);

        if (s3ClientResult.Client is null)
        {
            throw new InvalidOperationException(
                $"AWS S3 client was null despite successful connection test (Code: {s3ClientResult.Code}). This should never happen.");
        }

        return new StorageClientFactoryResult(
            Code: StorageOperationResultCode.Ok,
            Details: new StoragePreparationDetails
            {
                StorageType = StorageType.AwsS3,
                DetailsJson = Json.Serialize(input),
                StorageClientFactory = clientDetails => new S3StorageClient(
                    appUrl: config.AppUrl,
                    clock: clock,
                    s3Client: s3ClientResult.Client,
                    storageId: clientDetails.StorageId,
                    externalId: clientDetails.ExternalId,
                    storageType: StorageType.AwsS3,
                    preSignedUrlsService: preSignedUrlsService,
                    encryptionType: clientDetails.EncryptionType,
                    encryptionDetails: clientDetails.EncryptionDetails)
            });
    }
}
