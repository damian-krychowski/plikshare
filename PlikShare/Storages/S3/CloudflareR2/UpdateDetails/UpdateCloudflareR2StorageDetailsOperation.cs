using PlikShare.Core.Clock;
using PlikShare.Core.Configuration;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Entities;
using PlikShare.Storages.Id;
using PlikShare.Storages.UpdateDetails;

namespace PlikShare.Storages.S3.CloudflareR2.UpdateDetails;

public class UpdateCloudflareR2StorageDetailsOperation(
    IMasterDataEncryption masterDataEncryption,
    IConfig config,
    IClock clock,
    StorageClientStore storageClientStore,
    UpdateStorageDetailsQuery updateStorageDetailsQuery,
    PreSignedUrlsService preSignedUrlsService)
{
    public async Task<ResultCode> Execute(
        StorageExtId externalId,
        CloudflareR2DetailsEntity newDetails,
        CancellationToken cancellationToken = default)
    {
        var newClientResult = await S3Client.BuildCloudflareAndTestConnection(
            accessKeyId: newDetails.AccessKeyId,
            secretAccessKey: newDetails.SecretAccessKey,
            url: newDetails.Url,
            cancellationToken: cancellationToken);

        if (newClientResult.Code == S3Client.CloudflareResultCode.InvalidUrl)
            return ResultCode.InvalidUrl;

        if (newClientResult.Code == S3Client.CloudflareResultCode.CouldNotConnect)
            return ResultCode.CouldNotConnect;
        
        var result = await updateStorageDetailsQuery.Execute(
            externalId: externalId,
            storageType: StorageType.CloudflareR2,
            detailsJson: Json.Serialize(item: newDetails),
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case UpdateStorageDetailsQuery.ResultCode.Ok:
                RegisterClient(externalId, result, newClientResult);

                return ResultCode.Ok;
            
            case UpdateStorageDetailsQuery.ResultCode.NotFound:
                return ResultCode.NotFound;
            
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void RegisterClient(
        StorageExtId externalId, 
        UpdateStorageDetailsQuery.Result result, 
        S3Client.CloudflareResult newClientResult)
    {
        var encryptionDetails = result.StorageData?.EncryptionDetailsEncrypted is null
            ? null
            : StorageEncryptionExtensions.GetEncryptionDetails(
                encryptionType: result.StorageData.EncryptionType,
                encryptionDetailsJson: masterDataEncryption.Decrypt(
                    result.StorageData.EncryptionDetailsEncrypted));

        storageClientStore.RegisterClient(new S3StorageClient(
            appUrl: config.AppUrl,
            clock: clock,
            s3Client: newClientResult.Client!,
            storageId: result.StorageData!.Id,
            externalId: externalId,
            storageType: StorageType.CloudflareR2,
            preSignedUrlsService: preSignedUrlsService,
            encryptionType: result.StorageData.EncryptionType,
            encryptionDetails: encryptionDetails));
    }

    public enum ResultCode
    {
        Ok,
        NotFound,
        CouldNotConnect,
        InvalidUrl
    }
}