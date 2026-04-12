using PlikShare.Core.Encryption;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;

namespace PlikShare.Storages.UpdateDetails;

public class UpdateStorageFlow(
    IMasterDataEncryption masterDataEncryption,
    UpdateStorageDetailsQuery updateStorageDetailsQuery,
    StorageClientStore storageClientStore)
{
    public async Task<Result> Execute<TInput>(
        IStorageClientFactory<TInput> factory,
        StorageExtId externalId,
        TInput input,
        CancellationToken cancellationToken)
    {
        var preparation = await factory.Prepare(
            input: input,
            cancellationToken: cancellationToken);

        if (preparation.Code != StorageOperationResultCode.Ok)
            return new Result(Code: preparation.Code);

        if (preparation.Details is null)
        {
            throw new InvalidOperationException(
                $"StorageClientFactoryResult.Details was null for successful preparation (Code: {preparation.Code}). This should never happen.");
        }

        var queryResult = await updateStorageDetailsQuery.Execute(
            externalId: externalId,
            storageType: preparation.Details.StorageType,
            detailsJson: preparation.Details.DetailsJson,
            cancellationToken: cancellationToken);

        if (queryResult.Code == UpdateStorageDetailsQuery.ResultCode.NotFound)
            return new Result(Code: StorageOperationResultCode.NotFound);

        var storageData = queryResult.StorageData!;

        var encryptionDetails = storageData.EncryptionDetailsEncrypted is null
            ? null
            : StorageEncryptionExtensions.GetEncryptionDetails(
                encryptionType: storageData.EncryptionType,
                encryptionDetailsJson: masterDataEncryption.Decrypt(
                    storageData.EncryptionDetailsEncrypted));

        var storageClientDetails = new StorageClientDetails
        {
            StorageId = storageData.Id,
            ExternalId = externalId,
            EncryptionType = storageData.EncryptionType,
            EncryptionDetails = encryptionDetails
        };

        var client = preparation.Details.StorageClientFactory(
            storageClientDetails);

        storageClientStore.RegisterClient(client);

        return new Result(
            Code: StorageOperationResultCode.Ok,
            Name: storageData.Name);
    }

    public record Result(
        StorageOperationResultCode Code,
        string? Name = null);
}
