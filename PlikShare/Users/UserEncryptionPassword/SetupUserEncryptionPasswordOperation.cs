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
    UserEncryptionDataReader userEncryptionDataReader,
    UpsertUserEncryptionDataQuery upsertUserEncryptionDataQuery)
{
    public async Task<Result> Execute(
        UserContext user,
        string encryptionPassword,
        CancellationToken cancellationToken)
    {
        if (userEncryptionDataReader.LoadForUser(user.Id) is not null)
        {
            Log.Warning("User '{UserId}' already has encryption configured — setup rejected.", user.Id);
            return new Result(ResultCode.AlreadyConfigured);
        }

        var keypair = UserKeyPair.Generate();

        var kdfSalt = EncryptionPasswordKdf.GenerateSalt();
        var kdfParams = EncryptionPasswordKdf.Params.Default;
        var passwordKek = EncryptionPasswordKdf.DeriveKek(encryptionPassword, kdfSalt, kdfParams);
        var verifyHash = EncryptionPasswordKdf.ComputeVerifyHash(passwordKek);
        var encryptedPrivateKey = WrappedPrivateKey.Wrap(passwordKek, keypair.PrivateKey);

        var recoverySeed = UserEncryptionRecovery.GenerateRecoverySeed();
        var recoveryKek = UserEncryptionRecovery.DeriveRecoveryKek(recoverySeed);
        var recoveryWrappedPrivateKey = WrappedPrivateKey.Wrap(recoveryKek, keypair.PrivateKey);
        var recoveryVerifyHash = UserEncryptionRecovery.ComputeVerifyHash(recoverySeed);

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

        var recoveryCode = RecoveryCodeCodec.Encode(recoverySeed);

        return new Result(
            Code: ResultCode.Ok,
            PrivateKey: keypair.PrivateKey,
            RecoveryCode: recoveryCode);
    }

    public enum ResultCode
    {
        Ok = 0,
        AlreadyConfigured,
        UserNotFound
    }

    public readonly record struct Result(
        ResultCode Code,
        byte[]? PrivateKey = null,
        string? RecoveryCode = null);
}
