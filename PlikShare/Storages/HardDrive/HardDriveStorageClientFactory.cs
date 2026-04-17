using PlikShare.Core.Clock;
using PlikShare.Core.Utils;
using PlikShare.Core.Volumes;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Storages.Entities;
using PlikShare.Storages.HardDrive.StorageClient;

namespace PlikShare.Storages.HardDrive;

public class HardDriveStorageClientFactory(
    PreSignedUrlsService preSignedUrlsService,
    Volumes volumes,
    IClock clock) : IStorageClientFactory<HardDriveStorageClientFactory.Input>
{
    public Task<StorageClientFactoryResult> Prepare(
        Input input,
        CancellationToken cancellationToken)
    {
        if (!volumes.TryGetVolumeLocationByVolumePath(input.VolumePath, out var volumeLocation))
        {
            return Task.FromResult(new StorageClientFactoryResult(
                Code: StorageOperationResultCode.VolumeNotFound));
        }

        var details = new HardDriveDetailsEntity(
            VolumePath: Location.NormalizePath(input.VolumePath),
            FolderPath: Location.NormalizePath(input.FolderPath),
            FullPath: volumeLocation.Combine(input.FolderPath).FullPath);

        return Task.FromResult(new StorageClientFactoryResult(
            Code: StorageOperationResultCode.Ok,
            Details: new StoragePreparationDetails
            {
                StorageType = StorageType.HardDrive,
                DetailsJson = Json.Serialize(details),
                StorageClientFactory = clientDetails => new HardDriveStorageClient(
                    preSignedUrlsService: preSignedUrlsService,
                    clock: clock,
                    details: details,
                    storageId: clientDetails.StorageId,
                    externalId: clientDetails.ExternalId,
                    name: clientDetails.Name,
                    encryptionType: clientDetails.EncryptionType,
                    encryptionDetails: clientDetails.EncryptionDetails,
                    encryption: clientDetails.Encryption)
            }));
    }

    public record Input(
        string VolumePath,
        string FolderPath);
}
