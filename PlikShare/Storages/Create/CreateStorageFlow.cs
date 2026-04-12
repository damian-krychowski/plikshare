using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;

namespace PlikShare.Storages.Create;

public class CreateStorageFlow(
    CreateStorageQuery createStorageQuery,
    StorageClientStore storageClientStore)
{
    public async Task<Result> Execute<TInput>(
        IStorageCreator<TInput> creator,
        TInput input,
        string name,
        StorageEncryptionType encryptionType,
        CancellationToken cancellationToken)
    {
        var preparation = await creator.Prepare(
            input: input,
            cancellationToken: cancellationToken);

        if (preparation.Code != StorageCreationResultCode.Ok)
            return new Result(Code: preparation.Code);

        if (preparation.Details is null)
        {
            throw new InvalidOperationException(
                $"StoragePreparation.Details was null for successful preparation (Code: {preparation.Code}). This should never happen.");
        }

        var encryptionDetails = StorageEncryptionExtensions.PrepareEncryptionDetails(
            encryptionType: encryptionType);

        var queryResult = await createStorageQuery.Execute(
            name: name,
            storageType: preparation.Details.StorageType,
            detailsJson: preparation.Details.DetailsJson,
            encryptionType: encryptionType,
            encryptionDetails: encryptionDetails,
            cancellationToken: cancellationToken);

        if (queryResult.Code == CreateStorageQuery.ResultCode.NameNotUnique)
            return new Result(Code: StorageCreationResultCode.NameNotUnique);

        var storageClientDetails = new StorageClientDetails
        {
            StorageId = queryResult.StorageId,
            ExternalId = queryResult.StorageExternalId,
            EncryptionType = encryptionType,
            EncryptionDetails = encryptionDetails
        };

        var client = preparation.Details.StorageClientFactory(
            storageClientDetails);

        storageClientStore.RegisterClient(client);

        return new Result(
            Code: StorageCreationResultCode.Ok,
            StorageExternalId: queryResult.StorageExternalId);
    }

    public record Result(
        StorageCreationResultCode Code,
        StorageExtId? StorageExternalId = null);
}
