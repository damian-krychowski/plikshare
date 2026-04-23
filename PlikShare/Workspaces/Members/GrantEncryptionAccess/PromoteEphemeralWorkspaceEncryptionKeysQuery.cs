using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Workspaces.Encryption;
using Serilog;

namespace PlikShare.Workspaces.Members.GrantEncryptionAccess;

/// <summary>
/// Promotes every ephemeral wrap staged for a user to a normal per-user <c>wek_*</c> wrap,
/// called exactly once during the user's encryption-password setup when they registered
/// using an invitation code. Resolves the shared ephemeral private key from
/// <c>euek_ephemeral_user_encryption_keys</c> (unwrapped with the invitation code), opens
/// each <c>ewek_*</c> sealed DEK with it, re-seals to the user's freshly-generated real
/// public key, upserts into <c>wek_*</c>, and finally wipes both the ewek rows and the euek
/// row. Caller provides an open transaction so the entire promotion is atomic with the
/// <c>u_users</c> encryption-metadata write.
///
/// The invitation code validity is asserted at sign-up (hash match against
/// <c>u_user_invitations</c>); if either the private-key unwrap or a DEK open fails here
/// the DB is corrupt or the wrong bytes were passed — we throw and the caller rolls back.
/// A present euek row with no ewek rows is a degenerate but not-erroneous state
/// (e.g. all invitations TTL-expired before setup) — we still wipe the euek row and return 0.
/// </summary>
public class PromoteEphemeralWorkspaceEncryptionKeysQuery(
    UpsertWorkspaceEncryptionKeyQuery upsertWorkspaceEncryptionKeyQuery)
{
    public int ExecuteTransaction(
        SqliteWriteContext dbWriteContext,
        int userId,
        byte[] publicKey,
        byte[] invitationCodeBytes,
        SqliteTransaction transaction)
    {
        var euekRow = dbWriteContext
            .OneRowCmd(
                sql: """
                     SELECT euek_encrypted_private_key
                     FROM euek_ephemeral_user_encryption_keys
                     WHERE euek_user_id = $userId
                     """,
                readRowFunc: reader => reader.GetFieldValue<byte[]>(0),
                transaction: transaction)
            .WithParameter("$userId", userId)
            .Execute();

        if (euekRow.IsEmpty)
            return 0;

        var ewekRows = dbWriteContext
            .Cmd(
                sql: """
                     SELECT
                         ewek_workspace_id,
                         ewek_storage_dek_version,
                         ewek_encrypted_workspace_dek,
                         ewek_created_by_user_id
                     FROM ewek_ephemeral_workspace_encryption_keys
                     WHERE ewek_user_id = $userId
                     """,
                readRowFunc: reader => new EphemeralRow(
                    WorkspaceId: reader.GetInt32(0),
                    StorageDekVersion: reader.GetInt32(1),
                    EncryptedWorkspaceDek: reader.GetFieldValue<byte[]>(2),
                    CreatedByUserId: reader.IsDBNull(3) ? null : reader.GetInt32(3)),
                transaction: transaction)
            .WithParameter("$userId", userId)
            .Execute();

        if (ewekRows.Count > 0)
        {
            using var ephemeralPrivateKey = InvitationCodePrivateKeyWrap.Unwrap(
                invitationCodeBytes,
                euekRow.Value);

            foreach (var row in ewekRows)
            {
                using var dek = UserKeyPair.OpenSealed(
                    ephemeralPrivateKey,
                    row.EncryptedWorkspaceDek);

                var sealedDek = dek.Use(
                    state: publicKey,
                    action: static (dekSpan, pubKey) => UserKeyPair.SealTo(pubKey, dekSpan));

                upsertWorkspaceEncryptionKeyQuery.ExecuteTransaction(
                    dbWriteContext: dbWriteContext,
                    workspaceId: row.WorkspaceId,
                    userId: userId,
                    storageDekVersion: row.StorageDekVersion,
                    wrappedWorkspaceDek: sealedDek,
                    wrappedByUserId: row.CreatedByUserId,
                    transaction: transaction);
            }

            dbWriteContext
                .Cmd(
                    sql: """
                         DELETE FROM ewek_ephemeral_workspace_encryption_keys
                         WHERE ewek_user_id = $userId
                         """,
                    readRowFunc: static reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$userId", userId)
                .Execute();
        }

        dbWriteContext
            .Cmd(
                sql: """
                     DELETE FROM euek_ephemeral_user_encryption_keys
                     WHERE euek_user_id = $userId
                     """,
                readRowFunc: static reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$userId", userId)
            .Execute();

        Log.Information(
            "User#{UserId} promoted {PromotedCount} ephemeral workspace encryption key(s) to normal wraps during encryption-password setup; ephemeral keypair wiped.",
            userId, ewekRows.Count);

        return ewekRows.Count;
    }

    private readonly record struct EphemeralRow(
        int WorkspaceId,
        int StorageDekVersion,
        byte[] EncryptedWorkspaceDek,
        int? CreatedByUserId);
}
