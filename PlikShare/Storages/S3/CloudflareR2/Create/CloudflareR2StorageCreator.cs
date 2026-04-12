using PlikShare.Core.Clock;
using PlikShare.Core.Configuration;
using PlikShare.Core.Utils;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Storages.Create;
using PlikShare.Storages.Entities;

namespace PlikShare.Storages.S3.CloudflareR2.Create;

public class CloudflareR2StorageCreator(
    IConfig config,
    IClock clock,
    PreSignedUrlsService preSignedUrlsService) : IStorageCreator<CloudflareR2DetailsEntity>
{
    public async Task<StoragePreparation> Prepare(
        CloudflareR2DetailsEntity input,
        CancellationToken cancellationToken)
    {
        var s3ClientResult = await S3Client.BuildCloudflareAndTestConnection(
            accessKeyId: input.AccessKeyId,
            secretAccessKey: input.SecretAccessKey,
            url: input.Url,
            cancellationToken: cancellationToken);

        if (s3ClientResult.Code == S3Client.CloudflareResultCode.InvalidUrl)
            return new StoragePreparation(Code: StorageCreationResultCode.InvalidUrl);

        if (s3ClientResult.Code == S3Client.CloudflareResultCode.CouldNotConnect)
            return new StoragePreparation(Code: StorageCreationResultCode.CouldNotConnect);

        if (s3ClientResult.Client is null)
        {
            throw new InvalidOperationException(
                $"Cloudflare R2 S3 client was null despite successful connection test (Code: {s3ClientResult.Code}). This should never happen.");
        }

        return new StoragePreparation(
            Code: StorageCreationResultCode.Ok,
            Details: new StoragePreparationDetails
            {
                StorageType = StorageType.CloudflareR2,
                DetailsJson = Json.Serialize(input),
                StorageClientFactory = details => new S3StorageClient(
                    appUrl: config.AppUrl,
                    clock: clock,
                    s3Client: s3ClientResult.Client,
                    storageId: details.StorageId,
                    externalId: details.ExternalId,
                    storageType: Entities.StorageType.CloudflareR2,
                    preSignedUrlsService: preSignedUrlsService,
                    encryptionType: details.EncryptionType,
                    encryptionDetails: details.EncryptionDetails)
            });
    }
}
