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
/// using an invitation code. For each <c>ewek_*</c> row: unwraps the DEK with the
/// invitation-code-derived KEK, seals it to the user's freshly-generated public key, upserts
/// into <c>wek_*</c>, deletes the ephemeral row. Caller provides an open transaction so
/// the entire promotion is atomic with the <c>u_users</c> encryption-metadata write.
///
/// The invitation code validity is asserted at sign-up (hash match against
/// <c>u_user_invitations</c>); if unwrap fails here the DB is corrupt or the wrong bytes
/// were passed — we throw and the caller rolls back.
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
        var rows = dbWriteContext
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

        if (rows.Count == 0)
            return 0;

        foreach (var row in rows)
        {
            using var dek = InvitationCodeDekWrap.Unwrap(
                invitationCodeBytes,
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

        var deletedCount = dbWriteContext
            .Cmd(
                sql: """
                     DELETE FROM ewek_ephemeral_workspace_encryption_keys
                     WHERE ewek_user_id = $userId
                     """,
                readRowFunc: static reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$userId", userId)
            .Execute()
            .Count;

        Log.Information(
            "User#{UserId} promoted {PromotedCount} ephemeral workspace encryption key(s) to normal wraps during encryption-password setup.",
            userId, rows.Count);

        return rows.Count;
    }

    private readonly record struct EphemeralRow(
        int WorkspaceId,
        int StorageDekVersion,
        byte[] EncryptedWorkspaceDek,
        int? CreatedByUserId);
}
