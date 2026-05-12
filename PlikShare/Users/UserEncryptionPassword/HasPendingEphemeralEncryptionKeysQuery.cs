using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.Users.UserEncryptionPassword;

/// <summary>
/// Returns true when the user has an <c>euek_*</c> row staged for them — i.e. someone
/// invited them to a full-encryption workspace as a brand-new invitee and the
/// promotion to a real <c>wek_*</c> wrap is still waiting on the user setting up an
/// encryption password. After <see cref="SetupUserEncryptionPasswordOperation"/>
/// completes, the row is wiped and this flips to false.
///
/// Used by the sign-up flow to decide whether to open the encryption-password
/// setup dialog right after first sign-in: opening it for admin-only invitees
/// (no workspace involvement, no euek row) is meaningless UX.
/// </summary>
public class HasPendingEphemeralEncryptionKeysQuery(PlikShareDb plikShareDb)
{
    public bool Execute(int userId)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: """
                     SELECT 1
                     FROM euek_ephemeral_user_encryption_keys
                     WHERE euek_user_id = $userId
                     LIMIT 1
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$userId", userId)
            .Execute();

        return !result.IsEmpty;
    }
}
