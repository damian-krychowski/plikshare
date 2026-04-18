using PlikShare.Core.Clock;
using PlikShare.Core.Configuration;
using PlikShare.Core.Encryption;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Storages.Entities;

namespace PlikShare.Storages.S3.DigitalOcean;

public class DigitalOceanStorageClientFactory(
    IMasterDataEncryption masterDataEncryption,
    IConfig config,
    IClock clock,
    PreSignedUrlsService preSignedUrlsService) : IStorageClientFactory<DigitalOceanSpacesDetailsEntity>
{
    public async Task<StorageClientFactoryResult> Prepare(
        DigitalOceanSpacesDetailsEntity input,
        CancellationToken cancellationToken)
    {
        var (code, client) = await S3Client.BuildDigitalOceanSpacesAndTestConnection(
            accessKey: input.AccessKey,
            secretKey: input.SecretKey,
            url: input.Url,
            cancellationToken: cancellationToken);

        if (code == S3Client.DigitalOceanSpacesResultCode.InvalidUrl)
            return new StorageClientFactoryResult(Code: StorageOperationResultCode.InvalidUrl);

        if (code == S3Client.DigitalOceanSpacesResultCode.CouldNotConnect)
            return new StorageClientFactoryResult(Code: StorageOperationResultCode.CouldNotConnect);

        if (client is null)
        {
            throw new InvalidOperationException(
                $"DigitalOcean Spaces S3 client was null despite successful connection test (Code: {code}). This should never happen.");
        }

        return new StorageClientFactoryResult(
            Code: StorageOperationResultCode.Ok,
            Details: StoragePreparationDetails.Prepare(
                masterDataEncryption: masterDataEncryption,
                config: config,
                clock: clock,
                preSignedUrlsService: preSignedUrlsService,
                client: client,
                input: input,
                storageType: StorageType.DigitalOceanSpaces));
    }
}
