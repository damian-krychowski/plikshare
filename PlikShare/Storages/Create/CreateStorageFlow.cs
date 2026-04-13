using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;

namespace PlikShare.Storages.Create;

public class CreateStorageFlow(
    CreateStorageQuery createStorageQuery,
    StorageClientStore storageClientStore)
{
    public async Task<Result> Execute<TInput>(
        IStorageClientFactory<TInput> factory,
        TInput input,
        string name,
        StorageEncryptionType encryptionType,
        string? masterPassword,
        CancellationToken cancellationToken)
    {
        if (encryptionType == StorageEncryptionType.Full && string.IsNullOrEmpty(masterPassword))
            return new Result(Code: StorageOperationResultCode.MasterPasswordRequired);

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

        StorageEncryptionDetails? encryptionDetails;
        string? recoveryCode = null;

        switch (encryptionType)
        {
            case StorageEncryptionType.None:
                encryptionDetails = null;
                break;

            case StorageEncryptionType.Managed:
                encryptionDetails = StorageEncryptionExtensions
                    .PrepareManagedEncryptionDetails();
                break;

            case StorageEncryptionType.Full:
                var fullResult = StorageFullEncryptionService.GenerateDetails(masterPassword!);
                encryptionDetails = fullResult.Details;
                recoveryCode = fullResult.RecoveryCode;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(encryptionType), encryptionType, null);
        }

        var queryResult = await createStorageQuery.Execute(
            name: name,
            storageType: preparation.Details.StorageType,
            detailsJson: preparation.Details.DetailsJson,
            encryptionType: encryptionType,
            encryptionDetails: encryptionDetails,
            cancellationToken: cancellationToken);

        if (queryResult.Code == CreateStorageQuery.ResultCode.NameNotUnique)
            return new Result(Code: StorageOperationResultCode.NameNotUnique);

        var storageClientDetails = new StorageClientDetails
        {
            StorageId = queryResult.StorageId,
            ExternalId = queryResult.StorageExternalId,
            Name = name,
            EncryptionType = encryptionType,
            EncryptionDetails = encryptionDetails
        };

        var client = preparation.Details.StorageClientFactory(
            storageClientDetails);

        storageClientStore.RegisterClient(client);

        return new Result(
            Code: StorageOperationResultCode.Ok,
            StorageExternalId: queryResult.StorageExternalId,
            RecoveryCode: recoveryCode);
    }

    public record Result(
        StorageOperationResultCode Code,
        StorageExtId? StorageExternalId = null,
        string? RecoveryCode = null);
}
