using PlikShare.Core.Clock;
using PlikShare.Core.Configuration;
using PlikShare.Core.Utils;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Storages.Create;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Entities;
using PlikShare.Storages.Id;

namespace PlikShare.Storages.S3.AwsS3.Create;

public class CreateAwsS3StorageOperation(
    IConfig config,
    IClock clock,
    StorageClientStore storageClientStore,
    CreateStorageQuery createStorageQuery,
    PreSignedUrlsService preSignedUrlsService)
{
    public async Task<Result> Execute(
        string name,
        AwsS3DetailsEntity details,
        StorageEncryptionType encryptionType,
        CancellationToken cancellationToken = default)
    {
        var s3ClientResult = await S3Client.BuildAwsAndTestConnection(
            accessKey: details.AccessKey,
            secretAccessKey: details.SecretAccessKey,
            region: details.Region,
            cancellationToken: cancellationToken);

        // if (s3ClientResult.Code == S3Client.AwsResultCode.InvalidUrl)
        //     return new Result(Code: ResultCode.InvalidUrl);

        if (s3ClientResult.Code == S3Client.AwsResultCode.CouldNotConnect)
            return new Result(Code: ResultCode.CouldNotConnect);

        var encryptionDetails = StorageEncryptionExtensions.PrepareEncryptionDetails(
            encryptionType: encryptionType);

        var queryResult = await createStorageQuery.Execute(
            name: name,
            storageType: StorageType.AwsS3,
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
                storageType: StorageType.AwsS3,
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
        NameNotUnique
    }
    
    public record Result(
        ResultCode Code,
        StorageExtId? StorageExternalId = null);
}