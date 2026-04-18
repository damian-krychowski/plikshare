using Amazon.S3;
using PlikShare.Core.Clock;
using PlikShare.Core.Utils;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;
using PlikShare.Storages.S3;
using PlikShare.Core.Configuration;
using PlikShare.Core.Encryption;

namespace PlikShare.Storages;

public enum StorageOperationResultCode
{
    Ok,
    CouldNotConnect,
    InvalidUrl,
    VolumeNotFound,
    NameNotUnique,
    NotFound,
    CreatorEncryptionNotSetUp
}

public record StorageClientFactoryResult(
    StorageOperationResultCode Code,
    StoragePreparationDetails? Details = null);

public class StoragePreparationDetails
{
    public required string StorageType { get; init; }
    public required string DetailsJson { get; init; }
    public required Func<StorageClientDetails, IStorageClient> StorageClientFactory { get; init; }
}

public class StorageClientDetails
{
    public required int StorageId { get; init; }
    public required StorageExtId ExternalId { get; init; }
    public required string Name { get; init; }
    public required StorageEncryption Encryption { get; init; }
}

public interface IStorageClientFactory<TInput>
{
    public Task<StorageClientFactoryResult> Prepare(
        TInput input,
        CancellationToken cancellationToken);
}

public static class StoragePreparationDetailsExtensions
{
    extension(StoragePreparationDetails)
    {
        public static StoragePreparationDetails Prepare<TInput>(
            IMasterDataEncryption masterDataEncryption,
            IConfig config,
            IClock clock,
            PreSignedUrlsService preSignedUrlsService,
            IAmazonS3 client,
            TInput input,
            string storageType)
        {
            return new StoragePreparationDetails
            {
                StorageType = storageType,
                DetailsJson = Json.Serialize(input),
                StorageClientFactory = clientDetails => new S3StorageClient(
                    appUrl: config.AppUrl,
                    masterDataEncryption: masterDataEncryption,
                    clock: clock,
                    s3Client: client,
                    storageId: clientDetails.StorageId,
                    externalId: clientDetails.ExternalId,
                    name: clientDetails.Name,
                    preSignedUrlsService: preSignedUrlsService,
                    encryption: clientDetails.Encryption)
            };
        }
    }
}