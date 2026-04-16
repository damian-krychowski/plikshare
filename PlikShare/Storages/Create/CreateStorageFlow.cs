using System.Security.Cryptography;
using PlikShare.Core.Encryption;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;
using PlikShare.Users.Cache;
using PlikShare.Users.UserEncryptionPassword;

namespace PlikShare.Storages.Create;

public class CreateStorageFlow(
    CreateStorageQuery createStorageQuery,
    UserEncryptionDataReader userEncryptionDataReader,
    StorageClientStore storageClientStore)
{
    public async Task<Result> Execute<TInput>(
        IStorageClientFactory<TInput> factory,
        TInput input,
        string name,
        StorageEncryptionType encryptionType,
        UserContext creator,
        CancellationToken cancellationToken)
    {
        // For Full encryption we need the creator's public key up-front so we can wrap
        // the freshly generated Storage DEK to them. A creator who has not yet set up
        // an encryption password has no keypair and cannot own a full-encrypted storage.
        UserEncryptionData? creatorEncryption = null;
        if (encryptionType == StorageEncryptionType.Full)
        {
            creatorEncryption = userEncryptionDataReader.LoadForUser(
                creator.Id);

            if (creatorEncryption is null)
                return new Result(Code: StorageOperationResultCode.CreatorEncryptionNotSetUp);
        }

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
        byte[]? fullDekToWrap = null;

        switch (encryptionType)
        {
            case StorageEncryptionType.None:
                encryptionDetails = null;
                break;

            case StorageEncryptionType.Managed:
                var managedResult = StorageManagedEncryptionService.GenerateDetails();
                encryptionDetails = managedResult.Details;
                recoveryCode = managedResult.RecoveryCode;
                break;

            case StorageEncryptionType.Full:
                var fullResult = StorageFullEncryptionService.GenerateDetails();
                encryptionDetails = fullResult.Details;
                recoveryCode = fullResult.RecoveryCode;
                fullDekToWrap = fullResult.Dek;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(encryptionType), encryptionType, null);
        }

        try
        {
            CreatorEncryptionKeyData? creatorKeyData = null;

            if (encryptionType == StorageEncryptionType.Full)
            {
                var wrappedDek = UserKeyPair.SealTo(
                    recipientPublicKey: creatorEncryption!.PublicKey,
                    plaintext: fullDekToWrap!);

                creatorKeyData = new CreatorEncryptionKeyData(
                    UserId: creator.Id,
                    WrappedStorageDek: wrappedDek);
            }

            var queryResult = await createStorageQuery.Execute(
                name: name,
                storageType: preparation.Details.StorageType,
                detailsJson: preparation.Details.DetailsJson,
                encryptionType: encryptionType,
                encryptionDetails: encryptionDetails,
                creatorKeyData: creatorKeyData,
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
        finally
        {
            if (fullDekToWrap is not null)
                CryptographicOperations.ZeroMemory(fullDekToWrap);
        }
    }

    public record Result(
        StorageOperationResultCode Code,
        StorageExtId? StorageExternalId = null,
        string? RecoveryCode = null);
}
