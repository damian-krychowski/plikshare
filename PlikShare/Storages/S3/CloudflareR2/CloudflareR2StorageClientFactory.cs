using PlikShare.Core.Clock;
using PlikShare.Core.Configuration;
using PlikShare.Storages.Entities;

namespace PlikShare.Storages.S3.CloudflareR2;

public class CloudflareR2StorageClientFactory(
    IConfig config) : IStorageClientFactory<CloudflareR2DetailsEntity>
{
    public async Task<StorageClientFactoryResult> Prepare(
        CloudflareR2DetailsEntity input,
        CancellationToken cancellationToken)
    {
        var (code, client) = await S3Client.BuildCloudflareAndTestConnection(
            accessKeyId: input.AccessKeyId,
            secretAccessKey: input.SecretAccessKey,
            url: input.Url,
            cancellationToken: cancellationToken);

        if (code == S3Client.CloudflareResultCode.InvalidUrl)
            return new StorageClientFactoryResult(Code: StorageOperationResultCode.InvalidUrl);

        if (code == S3Client.CloudflareResultCode.CouldNotConnect)
            return new StorageClientFactoryResult(Code: StorageOperationResultCode.CouldNotConnect);

        if (client is null)
        {
            throw new InvalidOperationException(
                $"Cloudflare R2 S3 client was null despite successful connection test (Code: {code}). This should never happen.");
        }

        return new StorageClientFactoryResult(
            Code: StorageOperationResultCode.Ok,
            Details: StoragePreparationDetails.Prepare(
                config: config,
                client: client,
                input: input,
                storageType: StorageType.CloudflareR2,
                lifecycleRules:
                [
                    S3LifecycleRules.AbortIncompleteMultipartUploadsAfter7Days
                ]));
    }
}
