using PlikShare.Core.Clock;
using PlikShare.Core.Configuration;
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
        var (code, client) = await S3Client.BuildBackblazeAndTestConnection(
            keyId: input.KeyId,
            applicationKey: input.ApplicationKey,
            url: input.Url,
            cancellationToken: cancellationToken);

        if (code == S3Client.BackblazeResultCode.InvalidUrl)
            return new StorageClientFactoryResult(Code: StorageOperationResultCode.InvalidUrl);

        if (code == S3Client.BackblazeResultCode.CouldNotConnect)
            return new StorageClientFactoryResult(Code: StorageOperationResultCode.CouldNotConnect);

        if (client is null)
        {
            throw new InvalidOperationException(
                $"Backblaze B2 S3 client was null despite successful connection test (Code: {code}). This should never happen.");
        }

        return new StorageClientFactoryResult(
            Code: StorageOperationResultCode.Ok,
            Details: StoragePreparationDetails.Prepare(
                config: config,
                clock: clock,
                preSignedUrlsService: preSignedUrlsService,
                client: client,
                input: input,
                storageType: StorageType.BackblazeB2));
    }
}
