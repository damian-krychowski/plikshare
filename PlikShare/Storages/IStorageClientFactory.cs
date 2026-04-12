using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;

namespace PlikShare.Storages;

public enum StorageOperationResultCode
{
    Ok,
    CouldNotConnect,
    InvalidUrl,
    VolumeNotFound,
    NameNotUnique,
    NotFound
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
    public required StorageEncryptionType EncryptionType { get; init; }
    public required StorageManagedEncryptionDetails? EncryptionDetails { get; init; }
}

public interface IStorageClientFactory<TInput>
{
    public Task<StorageClientFactoryResult> Prepare(
        TInput input,
        CancellationToken cancellationToken);
}
