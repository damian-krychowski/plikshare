using PlikShare.Core.Encryption;
using PlikShare.Users.Cache;
using Serilog;

namespace PlikShare.Users.UserEncryptionPassword;

/// <summary>
/// Verifies an encryption password and unwraps the user's X25519 private key so the caller
/// can establish a <see cref="UserEncryptionSessionCookie"/>. Does not write to the DB —
/// this is a pure verify + unwrap operation.
/// </summary>
public class UnlockUserEncryptionPasswordOperation
{
    public async Task<Result> Execute(
        UserContext user,
        string encryptionPassword)
    {
        if (user.EncryptionMetadata is null)
        {
            Log.Debug("User '{UserId}' unlock rejected — encryption not configured.", user.Id);

            return new Result(ResultCode.NotConfigured);
        }

        using var kek = await EncryptionPasswordKdf.DeriveKek(
            encryptionPassword,
            user.EncryptionMetadata.KdfSalt,
            user.EncryptionMetadata.KdfParams);

        var isPasswordVerified = kek.Use(
            user.EncryptionMetadata.VerifyHash,
            static (kekSpan, expectedHash) => EncryptionPasswordKdf.Verify(kekSpan, expectedHash));

        if (!isPasswordVerified)
        {
            Log.Debug("User '{UserId}' unlock rejected — invalid encryption password.", user.Id);

            return new Result(ResultCode.InvalidPassword);
        }

        var privateKey = kek.Use(
            state: user.EncryptionMetadata.EncryptedPrivateKey,
            action: static (kekSpan, wrapped) => SymmetricAeadWrap.Unwrap(kekSpan, wrapped));

        return new Result(
            code: ResultCode.Ok,
            privateKey: privateKey);
    }

    public enum ResultCode
    {
        Ok = 0,
        NotConfigured,
        InvalidPassword
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