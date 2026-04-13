using Microsoft.AspNetCore.DataProtection;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;
using PlikShare.Storages.ResetMasterPassword;

namespace PlikShare.Storages.ChangeMasterPassword;

public class ChangeMasterPasswordOperation(
    StorageClientStore storageClientStore,
    UpdateStorageEncryptionDetailsQuery updateStorageEncryptionDetailsQuery,
    IDataProtectionProvider dataProtectionProvider)
{
    public async Task<Result> Execute(
        StorageExtId storageExternalId,
        string oldPassword,
        string newPassword,
        CancellationToken cancellationToken)
    {
        var storage = storageClientStore.TryGetClient(storageExternalId);

        if (storage is null)
            return new Result(ResultCode.StorageNotFound);

        if (storage.EncryptionType != StorageEncryptionType.Full)
            return new Result(ResultCode.EncryptionModeMismatch);

        var currentDetails = storage.EncryptionDetails?.Full
            ?? throw new InvalidOperationException(
                $"Storage '{storageExternalId}' has EncryptionType=Full but no FullEncryptionDetails configured.");

        var changeResult = StorageFullEncryptionService.TryChangeMasterPassword(
            oldPassword: oldPassword,
            newPassword: newPassword,
            currentDetails: currentDetails,
            dataProtectionProvider: dataProtectionProvider,
            dataProtectionPurpose: FullEncryptionSessionCookie.Purpose);

        switch (changeResult.Code)
        {
            case StorageFullEncryptionService.ChangePasswordResultCode.InvalidOldPassword:
                return new Result(ResultCode.InvalidOldPassword);

            case StorageFullEncryptionService.ChangePasswordResultCode.Ok:
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }

        var newDetails = changeResult.NewDetails!;

        var queryCode = await updateStorageEncryptionDetailsQuery.Execute(
            externalId: storageExternalId,
            newEncryptionDetails: newDetails,
            cancellationToken: cancellationToken);

        if (queryCode == UpdateStorageEncryptionDetailsQuery.ResultCode.NotFound)
            return new Result(ResultCode.StorageNotFound);

        storage.SetEncryptionDetails(newDetails);

        return new Result(
            Code: ResultCode.Ok,
            ProtectedKek: changeResult.ProtectedKek);
    }

    public enum ResultCode
    {
        Ok,
        StorageNotFound,
        EncryptionModeMismatch,
        InvalidOldPassword
    }

    public readonly record struct Result(
        ResultCode Code,
        string? ProtectedKek = null);
}
