using PlikShare.Core.Encryption;
using PlikShare.Users.Cache;
using Serilog;

namespace PlikShare.Users.UserEncryptionPassword;

/// <summary>
/// Verifies an encryption password and unwraps the user's X25519 private key so the caller
/// can establish a <see cref="UserEncryptionSessionCookie"/>. Does not write to the DB —
/// this is a pure verify + unwrap operation.
/// </summary>
public class UnlockUserEncryptionPasswordOperation(
    UserEncryptionDataReader userEncryptionDataReader)
{
    public Result Execute(UserContext user, string encryptionPassword)
    {
        var data = userEncryptionDataReader.LoadForUser(user.Id);

        if (data is null)
        {
            Log.Debug("User '{UserId}' unlock rejected — encryption not configured.", user.Id);
            return new Result(ResultCode.NotConfigured);
        }

        var kek = EncryptionPasswordKdf.DeriveKek(encryptionPassword, data.KdfSalt, data.KdfParams);

        if (!EncryptionPasswordKdf.Verify(kek, data.VerifyHash))
        {
            Log.Debug("User '{UserId}' unlock rejected — invalid encryption password.", user.Id);
            return new Result(ResultCode.InvalidPassword);
        }

        var privateKey = WrappedPrivateKey.Unwrap(kek, data.EncryptedPrivateKey);

        return new Result(
            Code: ResultCode.Ok,
            PrivateKey: privateKey);
    }

    public enum ResultCode
    {
        Ok = 0,
        NotConfigured,
        InvalidPassword
    }

    public readonly record struct Result(
        ResultCode Code,
        byte[]? PrivateKey = null);
}
