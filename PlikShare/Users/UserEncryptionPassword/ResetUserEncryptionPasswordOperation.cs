using PlikShare.Core.Encryption;
using PlikShare.Users.Cache;
using Serilog;

namespace PlikShare.Users.UserEncryptionPassword;

/// <summary>
/// Resets the user's encryption password via their recovery code. Decodes the BIP-39 code to
/// a recovery seed, verifies against the stored recovery verify hash, unwraps the private key
/// with the recovery-derived KEK, and re-wraps it with a freshly derived new password-KEK.
/// The recovery-wrapped blob and recovery verify hash are preserved — the recovery code
/// continues to work. Public key unchanged.
/// </summary>
public class ResetUserEncryptionPasswordOperation(
    UpsertUserEncryptionDataQuery upsertUserEncryptionDataQuery)
{
    public async Task<Result> Execute(
        UserContext user,
        string recoveryCode,
        string newPassword,
        CancellationToken cancellationToken)
    {
        if (user.EncryptionMetadata is null)
        {
            Log.Warning("User '{UserId}' password reset rejected — encryption not configured.", user.Id);

            return new Result(ResultCode.NotConfigured);
        }

        var decodeResult = RecoveryCodeCodec.TryDecode(
            recoveryCode,
            out var recoverySeed);

        if (decodeResult != RecoveryCodeCodec.DecodeResult.Ok)
        {
            Log.Warning(
                "User '{UserId}' password reset rejected — recovery code decode failed ({DecodeResult}).",
                user.Id,
                decodeResult);

            return new Result(ResultCode.InvalidRecoveryCode);
        }

        var isRecoverySeedMatching = UserEncryptionRecovery.Verify(
            recoverySeed,
            user.EncryptionMetadata.RecoveryVerifyHash);

        if (!isRecoverySeedMatching)
        {
            Log.Warning("User '{UserId}' password reset rejected — recovery code does not match.", user.Id);

            return new Result(ResultCode.InvalidRecoveryCode);
        }

        using var recoveryKek = UserEncryptionRecovery.DeriveRecoveryKek(
            recoverySeed);

        using var privateKey = recoveryKek.Use(
            state: user.EncryptionMetadata.RecoveryWrappedPrivateKey,
            static (kekSpan, wrapped) => SymmetricAeadWrap.Unwrap(kekSpan, wrapped));

        var newSalt = EncryptionPasswordKdf.GenerateSalt();

        var newParams = EncryptionPasswordKdf
            .Params
            .Default;

        using var newKek = await EncryptionPasswordKdf.DeriveKek(
            password: newPassword,
            salt: newSalt,
            parameters: newParams);

        var newVerifyHash = newKek.Use(
            static kekSpan => EncryptionPasswordKdf.ComputeVerifyHash(kekSpan));

        var newEncryptedPrivateKey = SecureBytes.UseBoth(
            first: newKek,
            second: privateKey,
            action: static (kekSpan, pkSpan) => SymmetricAeadWrap.Wrap(kekSpan, pkSpan));

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

        Log.Information("User '{UserId}' encryption password was reset via recovery code.", user.Id);

        return new Result(
            code: ResultCode.Ok,
            privateKey: privateKey.Clone());
    }

    public enum ResultCode
    {
        Ok = 0,
        NotConfigured,
        InvalidRecoveryCode,
        UserNotFound
    }

    public sealed class Result(
        ResultCode code,
        SecureBytes? privateKey = null) : IDisposable
    {
        public ResultCode Code { get; } = code;
        public SecureBytes? PrivateKey { get; } = privateKey;

        public void Dispose() => PrivateKey?.Dispose();
    }
}