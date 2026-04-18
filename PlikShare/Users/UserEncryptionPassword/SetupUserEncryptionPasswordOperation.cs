using PlikShare.Core.Encryption;
using PlikShare.Users.Cache;
using Serilog;

namespace PlikShare.Users.UserEncryptionPassword;

/// <summary>
/// First-time setup of a user's encryption password. Generates a fresh X25519 keypair,
/// a random recovery seed, and wraps the private key twice — once with the password-derived
/// KEK, once with the recovery-seed-derived KEK. Persists everything to u_users and returns
/// the BIP-39 recovery code for the caller to show to the user exactly once.
///
/// Rejects users who already have encryption configured — change/reset flows are separate.
/// </summary>
public class SetupUserEncryptionPasswordOperation(
    UpsertUserEncryptionDataQuery upsertUserEncryptionDataQuery)
{
    public async Task<Result> Execute(
        UserContext user,
        string encryptionPassword,
        CancellationToken cancellationToken)
    {
        if (user.EncryptionMetadata is not null)
        {
            Log.Warning("User '{UserId}' already has encryption configured — setup rejected.", user.Id);
            return new Result(ResultCode.AlreadyConfigured);
        }

        using var keypair = UserKeyPair.Generate();

        var kdfSalt = EncryptionPasswordKdf.GenerateSalt();
        var kdfParams = EncryptionPasswordKdf.Params.Default;

        using var passwordKek = await EncryptionPasswordKdf.DeriveKek(
            encryptionPassword, 
            kdfSalt, 
            kdfParams);

        var verifyHash = passwordKek.Use(
            static kekSpan => EncryptionPasswordKdf.ComputeVerifyHash(kekSpan));

        var encryptedPrivateKey = SecureBytes.UseBoth(
            first: keypair.PrivateKey,
            second: passwordKek,
            action: static (privateKeySpan, kekSpan) => WrappedPrivateKey.Wrap(kekSpan, privateKeySpan));

        var recoverySeed = UserEncryptionRecovery.GenerateRecoverySeed();

        using var recoveryKek = UserEncryptionRecovery.DeriveRecoveryKek(
            recoverySeed);

        var recoveryWrappedPrivateKey = SecureBytes.UseBoth(
            first: keypair.PrivateKey,
            second: recoveryKek,
            action: static (privateKeySpan, kekSpan) => WrappedPrivateKey.Wrap(kekSpan, privateKeySpan));

        var recoveryVerifyHash = UserEncryptionRecovery.ComputeVerifyHash(
            recoverySeed);

        var writeCode = await upsertUserEncryptionDataQuery.Execute(
            userId: user.Id,
            publicKey: keypair.PublicKey,
            encryptedPrivateKey: encryptedPrivateKey,
            kdfSalt: kdfSalt,
            kdfParams: kdfParams,
            verifyHash: verifyHash,
            recoveryWrappedPrivateKey: recoveryWrappedPrivateKey,
            recoveryVerifyHash: recoveryVerifyHash,
            cancellationToken: cancellationToken);

        if (writeCode == UpsertUserEncryptionDataQuery.ResultCode.UserNotFound)
            return new Result(ResultCode.UserNotFound);

        return new Result(
            code: ResultCode.Ok,
            privateKey: keypair.PrivateKey.Clone(),
            recoveryCode: RecoveryCodeCodec.Encode(recoverySeed));
    }

    public enum ResultCode
    {
        Ok = 0,
        AlreadyConfigured,
        UserNotFound
    }

    public sealed class Result(
        ResultCode code,
        SecureBytes? privateKey = null,
        string? recoveryCode = null) : IDisposable
    {
        public ResultCode Code { get; } = code;
        public SecureBytes? PrivateKey { get; } = privateKey;
        public string? RecoveryCode { get; } = recoveryCode;

        public void Dispose() => PrivateKey?.Dispose();
    }
}