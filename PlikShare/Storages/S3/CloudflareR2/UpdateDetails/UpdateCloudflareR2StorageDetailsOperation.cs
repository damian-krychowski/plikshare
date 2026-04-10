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
    public async Task<Result> Execute(
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
            return new Result(ResultCode.InvalidUrl);

        if (newClientResult.Code == S3Client.CloudflareResultCode.CouldNotConnect)
            return new Result(ResultCode.CouldNotConnect);

        var result = await updateStorageDetailsQuery.Execute(
            externalId: externalId,
            storageType: StorageType.CloudflareR2,
            detailsJson: Json.Serialize(item: newDetails),
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case UpdateStorageDetailsQuery.ResultCode.Ok:
                RegisterClient(externalId, result, newClientResult);

                return new Result(ResultCode.Ok, result.StorageData?.Name);

            case UpdateStorageDetailsQuery.ResultCode.NotFound:
                return new Result(ResultCode.NotFound);

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

    public readonly record struct Result(
        ResultCode Code,
        string? Name = null);

    public enum ResultCode
    {
        Ok,
        NotFound,
        CouldNotConnect,
        InvalidUrl
    }
}