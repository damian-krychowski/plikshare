using PlikShare.Core.Clock;
using PlikShare.Core.Configuration;
using PlikShare.Core.Encryption;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Storages.Entities;

namespace PlikShare.Storages.S3.AwsS3;

public class AwsS3StorageClientFactory(
    IMasterDataEncryption masterDataEncryption,
    IConfig config,
    IClock clock,
    PreSignedUrlsService preSignedUrlsService) : IStorageClientFactory<AwsS3DetailsEntity>
{
    public async Task<StorageClientFactoryResult> Prepare(
        AwsS3DetailsEntity input,
        CancellationToken cancellationToken)
    {
        var (code, client) = await S3Client.BuildAwsAndTestConnection(
            accessKey: input.AccessKey,
            secretAccessKey: input.SecretAccessKey,
            region: input.Region,
            cancellationToken: cancellationToken);

        if (code == S3Client.AwsResultCode.CouldNotConnect)
            return new StorageClientFactoryResult(Code: StorageOperationResultCode.CouldNotConnect);

        if (client is null)
        {
            throw new InvalidOperationException(
                $"AWS S3 client was null despite successful connection test (Code: {code}). This should never happen.");
        }

        return new StorageClientFactoryResult(
            Code: StorageOperationResultCode.Ok,
            Details: StoragePreparationDetails.Prepare(
                config: config,
                masterDataEncryption: masterDataEncryption,
                clock: clock,
                preSignedUrlsService: preSignedUrlsService,
                client: client,
                input: input,
                storageType: StorageType.AwsS3));
    }
}
