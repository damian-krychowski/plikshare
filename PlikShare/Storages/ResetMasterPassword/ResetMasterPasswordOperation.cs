using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;

namespace PlikShare.Storages.ResetMasterPassword;

public class ResetMasterPasswordOperation(
    StorageClientStore storageClientStore,
    UpdateStorageEncryptionDetailsQuery updateStorageEncryptionDetailsQuery)
{
    public async Task<ResultCode> Execute(
        StorageExtId storageExternalId,
        string recoveryCode,
        string newPassword,
        CancellationToken cancellationToken)
    {
        var storage = storageClientStore.TryGetClient(storageExternalId);

        if (storage is null)
            return ResultCode.StorageNotFound;

        if (storage.EncryptionType != StorageEncryptionType.Full)
            return ResultCode.EncryptionModeMismatch;

        var currentDetails = storage.EncryptionDetails?.Full
            ?? throw new InvalidOperationException(
                $"Storage '{storageExternalId}' has EncryptionType=Full but no FullEncryptionDetails configured.");

        var resetResult = StorageFullEncryptionService.TryResetMasterPasswordWithRecoveryCode(
            recoveryCode: recoveryCode,
            newPassword: newPassword,
            currentDetails: currentDetails);

        switch (resetResult.Code)
        {
            case StorageFullEncryptionService.ResetPasswordResultCode.MalformedRecoveryCode:
                return ResultCode.MalformedRecoveryCode;

            case StorageFullEncryptionService.ResetPasswordResultCode.InvalidRecoveryCode:
                return ResultCode.InvalidRecoveryCode;

            case StorageFullEncryptionService.ResetPasswordResultCode.Ok:
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }

        var newDetails = resetResult.NewDetails!;

        var queryCode = await updateStorageEncryptionDetailsQuery.Execute(
            externalId: storageExternalId,
            newEncryptionDetails: newDetails,
            cancellationToken: cancellationToken);

        if (queryCode == UpdateStorageEncryptionDetailsQuery.ResultCode.NotFound)
            return ResultCode.StorageNotFound;

        storage.SetEncryptionDetails(newDetails);

        return ResultCode.Ok;
    }

    public enum ResultCode
    {
        Ok,
        StorageNotFound,
        EncryptionModeMismatch,
        MalformedRecoveryCode,
        InvalidRecoveryCode
    }
}
