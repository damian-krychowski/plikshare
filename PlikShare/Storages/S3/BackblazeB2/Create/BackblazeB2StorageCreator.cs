using PlikShare.Core.Clock;
using PlikShare.Core.Configuration;
using PlikShare.Core.Utils;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Storages.Create;
using PlikShare.Storages.Entities;

namespace PlikShare.Storages.S3.BackblazeB2.Create;

public class BackblazeB2StorageCreator(
    IConfig config,
    IClock clock,
    PreSignedUrlsService preSignedUrlsService) : IStorageCreator<BackblazeB2DetailsEntity>
{
    public async Task<StoragePreparation> Prepare(
        BackblazeB2DetailsEntity input,
        CancellationToken cancellationToken)
    {
        var s3ClientResult = await S3Client.BuildBackblazeAndTestConnection(
            keyId: input.KeyId,
            applicationKey: input.ApplicationKey,
            url: input.Url,
            cancellationToken: cancellationToken);

        if (s3ClientResult.Code == S3Client.BackblazeResultCode.InvalidUrl)
            return new StoragePreparation(Code: StorageCreationResultCode.InvalidUrl);

        if (s3ClientResult.Code == S3Client.BackblazeResultCode.CouldNotConnect)
            return new StoragePreparation(Code: StorageCreationResultCode.CouldNotConnect);

        if (s3ClientResult.Client is null)
        {
            throw new InvalidOperationException(
                $"Backblaze B2 S3 client was null despite successful connection test (Code: {s3ClientResult.Code}). This should never happen.");
        }

        return new StoragePreparation(
            Code: StorageCreationResultCode.Ok,
            Details: new StoragePreparationDetails
            {
                StorageType = StorageType.BackblazeB2,
                DetailsJson = Json.Serialize(input),
                StorageClientFactory = clientDetails => new S3StorageClient(
                    appUrl: config.AppUrl,
                    clock: clock,
                    s3Client: s3ClientResult.Client,
                    storageId: clientDetails.StorageId,
                    externalId: clientDetails.ExternalId,
                    storageType: StorageType.BackblazeB2,
                    preSignedUrlsService: preSignedUrlsService,
                    encryptionType: clientDetails.EncryptionType,
                    encryptionDetails: clientDetails.EncryptionDetails)
            });
    }
}
