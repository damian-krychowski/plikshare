using PlikShare.Core.Clock;
using PlikShare.Core.Utils;
using PlikShare.Core.Volumes;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Storages.Create;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Entities;
using PlikShare.Storages.HardDrive.StorageClient;
using PlikShare.Storages.Id;

namespace PlikShare.Storages.HardDrive.Create;

public class CreateHardDriveStorageOperation(
    PreSignedUrlsService preSignedUrlsService,
    Volumes volumes,
    IClock clock,
    StorageClientStore storageClientStore,
    CreateStorageQuery createStorageQuery)
{
    public async Task<Result> Execute(
        string name,
        string volumePath,
        string folderPath,
        StorageEncryptionType encryptionType,
        CancellationToken cancellationToken)
    {
        if (!volumes.TryGetVolumeLocationByVolumePath(volumePath, out var volumeLocation))
        {
            return new Result(
                Code: ResultCode.VolumeNotFound);
        }

        var details = new HardDriveDetailsEntity(
            VolumePath: Location.NormalizePath(
                volumePath),
            FolderPath: Location.NormalizePath(
                folderPath),
            FullPath: volumeLocation
                .Combine(folderPath)
                .FullPath);

        var encryptionDetails = StorageEncryptionExtensions.PrepareEncryptionDetails(
            encryptionType: encryptionType);

        var queryResult = await createStorageQuery.Execute(
            name: name,
            storageType: StorageType.HardDrive,
            detailsJson: Json.Serialize(
                item: details),
            encryptionType: encryptionType,
            encryptionDetails: encryptionDetails,
            cancellationToken: cancellationToken);

        if (queryResult.Code == CreateStorageQuery.ResultCode.Ok)
        {
            storageClientStore.RegisterClient(new HardDriveStorageClient(
                preSignedUrlsService: preSignedUrlsService,
                details: details,
                storageId: queryResult.StorageId,
                externalId: queryResult.StorageExternalId,
                clock: clock,
                encryptionType: encryptionType,
                encryptionDetails: encryptionDetails));
            
            return new Result(
                Code: ResultCode.Ok,
                StorageExternalId: queryResult.StorageExternalId);
        }

        return new Result(
            Code: ResultCode.NameNotUnique);
    }
    
    public enum ResultCode
    {
        Ok,
        NameNotUnique,
        VolumeNotFound
    }
    
    public record Result(
        ResultCode Code,
        StorageExtId? StorageExternalId = null);
}