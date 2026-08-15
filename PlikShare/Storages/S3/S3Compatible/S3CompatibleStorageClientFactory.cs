using PlikShare.Core.Configuration;
using PlikShare.Storages.Entities;

namespace PlikShare.Storages.S3.S3Compatible;

public class S3CompatibleStorageClientFactory(
    IConfig config) : IStorageClientFactory<S3CompatibleDetailsEntity>
{
    public async Task<StorageClientFactoryResult> Prepare(
        S3CompatibleDetailsEntity input,
        CancellationToken cancellationToken)
    {
        var (code, client) = await S3Client.BuildS3CompatibleAndTestConnection(
            accessKey: input.AccessKey,
            secretKey: input.SecretKey,
            url: input.Url,
            region: input.Region,
            forcePathStyle: input.ForcePathStyle,
            cancellationToken: cancellationToken);

        if (code == S3Client.S3CompatibleResultCode.InvalidUrl)
            return new StorageClientFactoryResult(Code: StorageOperationResultCode.InvalidUrl);

        if (code == S3Client.S3CompatibleResultCode.CouldNotConnect)
            return new StorageClientFactoryResult(Code: StorageOperationResultCode.CouldNotConnect);

        if (client is null)
        {
            throw new InvalidOperationException(
                $"S3-compatible client was null despite successful connection test (Code: {code}). This should never happen.");
        }

        return new StorageClientFactoryResult(
            Code: StorageOperationResultCode.Ok,
            Details: StoragePreparationDetails.Prepare(
                config: config,
                client: client,
                input: input,
                storageType: StorageType.S3Compatible,
                // No lifecycle rules — support for lifecycle configuration varies
                // between self-hosted backends (Garage has none) and a failing
                // PutLifecycleConfiguration would permanently break workspace
                // bucket provisioning. Admins can configure lifecycle server-side.
                lifecycleRules: [],
                customCorsConfigurator: (bucketName, ct) => S3CompatibleCorsConfigurer.TryPutBucketCors(
                    client: client,
                    bucketName: bucketName,
                    allowedOrigin: config.AppUrl,
                    cancellationToken: ct)));
    }
}
