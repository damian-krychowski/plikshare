using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;

namespace PlikShare.Users.UserEncryptionPassword;

/// <summary>
/// Loads the full encryption-related state for a user from u_users. Returns null if the
/// user exists but has not yet set up encryption (all encryption columns NULL).
/// </summary>
public class UserEncryptionDataReader(PlikShareDb plikShareDb)
{
    public UserEncryptionData? LoadForUser(int userId)
    {
        using var connection = plikShareDb.OpenConnection();

        var (isEmpty, data) = connection
            .OneRowCmd(
                sql: """
                     SELECT
                         u_encryption_public_key,
                         u_encryption_encrypted_private_key,
                         u_encryption_kdf_salt,
                         u_encryption_kdf_params,
                         u_encryption_verify_hash,
                         u_encryption_recovery_wrapped_private_key,
                         u_encryption_recovery_verify_hash
                     FROM u_users
                     WHERE u_id = $userId
                     LIMIT 1
                     """,
                readRowFunc: reader =>
                {
                    var publicKey = reader.GetFieldValueOrNull<byte[]>(0);
                    if (publicKey is null)
                        return (UserEncryptionData?)null;

                    return new UserEncryptionData
                    {
                        PublicKey = publicKey,
                        EncryptedPrivateKey = reader.GetFieldValue<byte[]>(1),
                        KdfSalt = reader.GetFieldValue<byte[]>(2),
                        KdfParams = EncryptionPasswordKdf.DeserializeParams(reader.GetString(3)),
                        VerifyHash = reader.GetFieldValue<byte[]>(4),
                        RecoveryWrappedPrivateKey = reader.GetFieldValue<byte[]>(5),
                        RecoveryVerifyHash = reader.GetFieldValue<byte[]>(6)
                    };
                })
            .WithParameter("$userId", userId)
            .Execute();

        return isEmpty ? null : data;
    }
}

public sealed class UserEncryptionData
{
    public required byte[] PublicKey { get; init; }
    public required byte[] EncryptedPrivateKey { get; init; }
    public required byte[] KdfSalt { get; init; }
    public required EncryptionPasswordKdf.Params KdfParams { get; init; }
    public required byte[] VerifyHash { get; init; }
    public required byte[] RecoveryWrappedPrivateKey { get; init; }
    public required byte[] RecoveryVerifyHash { get; init; }
}
