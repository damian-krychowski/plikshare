using PlikShare.Core.Clock;
using PlikShare.Core.Configuration;
using PlikShare.Core.Utils;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Storages.Create;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Entities;
using PlikShare.Storages.Id;

namespace PlikShare.Storages.S3.CloudflareR2.Create;

public class CreateCloudflareR2StorageOperation(
    IConfig config,
    IClock clock,
    StorageClientStore storageClientStore,
    CreateStorageQuery createStorageQuery,
    PreSignedUrlsService preSignedUrlsService)
{
    public async Task<Result> Execute(
        string name,
        CloudflareR2DetailsEntity details,
        StorageEncryptionType encryptionType,
        CancellationToken cancellationToken = default)
    {
        var s3ClientResult = await S3Client.BuildCloudflareAndTestConnection(
            accessKeyId: details.AccessKeyId,
            secretAccessKey: details.SecretAccessKey,
            url: details.Url,
            cancellationToken: cancellationToken);

        if (s3ClientResult.Code == S3Client.CloudflareResultCode.InvalidUrl)
            return new Result(Code: ResultCode.InvalidUrl);

        if (s3ClientResult.Code == S3Client.CloudflareResultCode.CouldNotConnect)
            return new Result(Code: ResultCode.CouldNotConnect);

        var encryptionDetails = StorageEncryptionExtensions.PrepareEncryptionDetails(
            encryptionType: encryptionType);

        var queryResult = await createStorageQuery.Execute(
            name: name,
            storageType: StorageType.CloudflareR2,
            detailsJson: Json.Serialize(item: details),
            encryptionType: encryptionType, 
            encryptionDetails: encryptionDetails,
            cancellationToken: cancellationToken);

        if (queryResult.Code == CreateStorageQuery.ResultCode.Ok)
        {
            storageClientStore.RegisterClient(new S3StorageClient(
                appUrl: config.AppUrl,
                clock: clock,
                s3Client: s3ClientResult.Client!,
                storageId: queryResult.StorageId,
                externalId: queryResult.StorageExternalId,
                storageType: StorageType.CloudflareR2,
                preSignedUrlsService: preSignedUrlsService,
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
        CouldNotConnect,
        NameNotUnique,
        InvalidUrl
    }
    
    public record Result(
        ResultCode Code,
        StorageExtId? StorageExternalId = null);
}