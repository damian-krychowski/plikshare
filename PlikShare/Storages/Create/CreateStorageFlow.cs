using System.Security.Cryptography;
using PlikShare.Core.Encryption;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;
using PlikShare.Users.Cache;
using PlikShare.Users.UserEncryptionPassword;

namespace PlikShare.Storages.Create;

public class CreateStorageFlow(
    CreateStorageQuery createStorageQuery,
    UpsertStorageEncryptionKeyQuery upsertStorageEncryptionKeyQuery,
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
            var queryResult = await createStorageQuery.Execute(
                name: name,
                storageType: preparation.Details.StorageType,
                detailsJson: preparation.Details.DetailsJson,
                encryptionType: encryptionType,
                encryptionDetails: encryptionDetails,
                cancellationToken: cancellationToken);

            if (queryResult.Code == CreateStorageQuery.ResultCode.NameNotUnique)
                return new Result(Code: StorageOperationResultCode.NameNotUnique);

            if (encryptionType == StorageEncryptionType.Full)
            {
                // Seal the Storage DEK to the creator's X25519 public key and record the
                // wrap in sek_storage_encryption_keys. Until this row exists the storage is
                // unusable (no one holds the DEK), so the creator is implicitly the first
                // storage admin once this write commits.
                var wrappedDek = UserKeyPair.SealTo(
                    recipientPublicKey: creatorEncryption!.PublicKey,
                    plaintext: fullDekToWrap!);

                await upsertStorageEncryptionKeyQuery.Execute(
                    storageId: queryResult.StorageId,
                    userId: creator.Id,
                    // v0 is the initial Storage DEK — HkdfDekDerivation.DeriveDek(recoverySeed, 0)
                    // gave us the DEK we are wrapping here. A future rotation will insert v1+ rows
                    // for every existing admin alongside this one.
                    storageDekVersion: 0,
                    wrappedStorageDek: wrappedDek,
                    wrappedByUserId: creator.Id,
                    cancellationToken: cancellationToken);
            }

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
