using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;

namespace PlikShare.Storages.Create;

public enum StorageCreationResultCode
{
    Ok,
    NameNotUnique,
    VolumeNotFound,
    CouldNotConnect,
    InvalidUrl
}

public record StoragePreparation(
    StorageCreationResultCode Code,
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

public interface IStorageCreator<TInput>
{
    public Task<StoragePreparation> Prepare(
        TInput input,
        CancellationToken cancellationToken);
}
