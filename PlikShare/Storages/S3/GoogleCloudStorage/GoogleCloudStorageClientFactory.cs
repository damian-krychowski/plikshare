using PlikShare.Core.Configuration;
using PlikShare.Storages.Entities;

namespace PlikShare.Storages.S3.GoogleCloudStorage;

public class GoogleCloudStorageClientFactory(
    IConfig config) : IStorageClientFactory<GoogleCloudStorageDetailsEntity>
{
    public async Task<StorageClientFactoryResult> Prepare(
        GoogleCloudStorageDetailsEntity input,
        CancellationToken cancellationToken)
    {
        var (code, client) = await S3Client.BuildGoogleCloudStorageAndTestConnection(
            accessKey: input.AccessKey,
            secretKey: input.SecretKey,
            cancellationToken: cancellationToken);

        if (code == S3Client.GoogleCloudStorageResultCode.CouldNotConnect)
            return new StorageClientFactoryResult(Code: StorageOperationResultCode.CouldNotConnect);

        if (client is null)
        {
            throw new InvalidOperationException(
                $"Google Cloud Storage S3 client was null despite successful connection test (Code: {code}). This should never happen.");
        }

        return new StorageClientFactoryResult(
            Code: StorageOperationResultCode.Ok,
            Details: StoragePreparationDetails.Prepare(
                config: config,
                client: client,
                input: input,
                storageType: StorageType.GoogleCloudStorage,
                lifecycleRules: [],
                customCorsConfigurator: (bucketName, ct) => GcsCorsConfigurer.PutBucketCorsAsync(
                    accessKey: input.AccessKey,
                    secretKey: input.SecretKey,
                    bucketName: bucketName,
                    allowedOrigin: config.AppUrl,
                    cancellationToken: ct)));
    }
}
