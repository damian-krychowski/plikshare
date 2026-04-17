using System.Security.Cryptography;
using PlikShare.Core.Authorization;
using PlikShare.Core.Encryption;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;
using PlikShare.Users.Cache;

namespace PlikShare.Storages.Create;

public class CreateStorageFlow(
    CreateStorageQuery createStorageQuery,
    AppOwners appOwners,
    UserCache userCache,
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
       if (encryptionType == StorageEncryptionType.Full && creator.EncryptionMetadata is null)
           return new Result(Code: StorageOperationResultCode.CreatorEncryptionNotSetUp);

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

        StorageEncryption encryption;
        StorageEncryptionDetails? encryptionDetails;
        string? recoveryCode = null;
        byte[]? fullDekToWrap = null;

        switch (encryptionType)
        {
            case StorageEncryptionType.None:
                encryptionDetails = null;
                encryption = NoStorageEncryption.Instance;
                break;

            case StorageEncryptionType.Managed:
                var managedResult = StorageManagedEncryptionService.GenerateDetails();
                encryptionDetails = managedResult.Details;
                recoveryCode = managedResult.RecoveryCode;
                encryption = new ManagedStorageEncryption(managedResult.Details);
                break;

            case StorageEncryptionType.Full:
                var fullResult = StorageFullEncryptionService.GenerateDetails();
                encryptionDetails = fullResult.Details;
                recoveryCode = fullResult.RecoveryCode;
                fullDekToWrap = fullResult.Dek;
                encryption = new FullStorageEncryption(fullResult.Details);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(encryptionType), encryptionType, null);
        }

        try
        {
            var ownerKeyDataList = encryptionType == StorageEncryptionType.Full
                ? await GetOwnerEncryptionKeyDataList(
                    creator,
                    fullDekToWrap,
                    cancellationToken)
                : [];

            var queryResult = await createStorageQuery.Execute(
                name: name,
                storageType: preparation.Details.StorageType,
                detailsJson: preparation.Details.DetailsJson,
                encryptionType: encryptionType,
                encryptionDetails: encryptionDetails,
                ownerKeyDataList: ownerKeyDataList,
                cancellationToken: cancellationToken);

            if (queryResult.Code == CreateStorageQuery.ResultCode.NameNotUnique)
                return new Result(Code: StorageOperationResultCode.NameNotUnique);

            var storageClientDetails = new StorageClientDetails
            {
                StorageId = queryResult.StorageId,
                ExternalId = queryResult.StorageExternalId,
                Name = name,
                EncryptionType = encryptionType,
                EncryptionDetails = encryptionDetails,
                Encryption = encryption
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

    private async Task<OwnerEncryptionKeyData[]> GetOwnerEncryptionKeyDataList(
        UserContext creator,
        byte[]? fullDekToWrap, 
        CancellationToken cancellationToken)
    {
        var owners = await appOwners.OwnerContexts(
            cache: userCache,
            cancellationToken: cancellationToken);

        return owners
            .Append(creator)
            .Where(user => user.EncryptionMetadata is not null)
            .DistinctBy(user => user.Id)
            .Select(user =>
            {
                var wrappedDek = UserKeyPair.SealTo(
                    recipientPublicKey: user.EncryptionMetadata!.PublicKey,
                    plaintext: fullDekToWrap!);

                return new OwnerEncryptionKeyData(
                    UserId: user.Id,
                    WrappedStorageDek: wrappedDek);
            })
            .ToArray();
    }

    public record Result(
        StorageOperationResultCode Code,
        StorageExtId? StorageExternalId = null,
        string? RecoveryCode = null);
}
