using PlikShare.Core.Encryption;
using PlikShare.Users.Cache;
using Serilog;

namespace PlikShare.Users.UserEncryptionPassword;

/// <summary>
/// Changes the user's encryption password. Unwraps the private key with the old password-KEK,
/// re-wraps with a freshly derived new password-KEK under a new salt + current default params.
/// The public key, recovery-wrapped private key, and recovery verify hash are preserved —
/// recovery code continues to work unchanged.
/// </summary>
public class ChangeUserEncryptionPasswordOperation(
    UpsertUserEncryptionDataQuery upsertUserEncryptionDataQuery)
{
    public async Task<Result> Execute(
        UserContext user,
        string oldPassword,
        string newPassword,
        CancellationToken cancellationToken)
    {
        if (user.EncryptionMetadata is null)
        {
            Log.Warning("User '{UserId}' password change rejected — encryption not configured.", user.Id);

            return new Result(ResultCode.NotConfigured);
        }

        var oldKek = EncryptionPasswordKdf.DeriveKek(
            oldPassword,
            user.EncryptionMetadata.KdfSalt,
            user.EncryptionMetadata.KdfParams);

        var isOldPasswordMatching = EncryptionPasswordKdf.Verify(
            oldKek,
            user.EncryptionMetadata.VerifyHash);

        if (!isOldPasswordMatching)
        {
            Log.Warning("User '{UserId}' password change rejected — invalid old password.", user.Id);

            return new Result(ResultCode.InvalidOldPassword);
        }

        var privateKey = WrappedPrivateKey.Unwrap(
            oldKek,
            user.EncryptionMetadata.EncryptedPrivateKey);

        var newSalt = EncryptionPasswordKdf.GenerateSalt();

        var newParams = EncryptionPasswordKdf
            .Params
            .Default;

        var newKek = EncryptionPasswordKdf.DeriveKek(
            newPassword, 
            newSalt, 
            newParams);

        var newVerifyHash = EncryptionPasswordKdf.ComputeVerifyHash(
            newKek);

        var newEncryptedPrivateKey = WrappedPrivateKey.Wrap(
            newKek, 
            privateKey);

        var writeCode = await upsertUserEncryptionDataQuery.Execute(
            userId: user.Id,
            publicKey: user.EncryptionMetadata.PublicKey,
            encryptedPrivateKey: newEncryptedPrivateKey,
            kdfSalt: newSalt,
            kdfParams: newParams,
            verifyHash: newVerifyHash,
            recoveryWrappedPrivateKey: user.EncryptionMetadata.RecoveryWrappedPrivateKey,
            recoveryVerifyHash: user.EncryptionMetadata.RecoveryVerifyHash,
            cancellationToken: cancellationToken);

        if (writeCode == UpsertUserEncryptionDataQuery.ResultCode.UserNotFound)
            return new Result(ResultCode.UserNotFound);

        Log.Information("User '{UserId}' encryption password was changed.", user.Id);

        return new Result(
            Code: ResultCode.Ok,
            PrivateKey: privateKey);
    }

    public enum ResultCode
    {
        Ok = 0,
        NotConfigured,
        InvalidOldPassword,
        UserNotFound
    }

    public readonly record struct Result(
        ResultCode Code,
        byte[]? PrivateKey = null);
}
