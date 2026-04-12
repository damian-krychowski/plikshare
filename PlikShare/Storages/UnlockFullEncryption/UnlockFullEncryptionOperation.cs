using Microsoft.AspNetCore.DataProtection;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;

namespace PlikShare.Storages.UnlockFullEncryption;

public class UnlockFullEncryptionOperation(
    StorageClientStore storageClientStore,
    IDataProtectionProvider dataProtectionProvider)
{
    public Result Execute(
        StorageExtId storageExternalId,
        string masterPassword)
    {
        var storage = storageClientStore.TryGetClient(
            storageExternalId);

        if (storage is null)
            return new Result(Code: ResultCode.StorageNotFound);

        if (storage.EncryptionType != StorageEncryptionType.Full)
            return new Result(Code: ResultCode.EncryptionModeMismatch);

        var encryptionDetails = storage.EncryptionDetails?.Full
            ?? throw new InvalidOperationException(
                $"Storage '{storageExternalId}' has EncryptionType=Full but no FullEncryptionDetails configured.");

        var protectedKek = StorageFullEncryptionService.TryUnlockProtectedKek(
            masterPassword: masterPassword,
            details: encryptionDetails,
            dataProtectionProvider: dataProtectionProvider,
            dataProtectionPurpose: FullEncryptionSessionCookie.Purpose);

        if (protectedKek is null)
            return new Result(Code: ResultCode.InvalidPassword);

        return new Result(
            Code: ResultCode.Ok,
            CookieValue: protectedKek);
    }

    public enum ResultCode
    {
        Ok,
        StorageNotFound,
        EncryptionModeMismatch,
        InvalidPassword
    }

    public readonly record struct Result(
        ResultCode Code,
        string? CookieValue = null);
}
