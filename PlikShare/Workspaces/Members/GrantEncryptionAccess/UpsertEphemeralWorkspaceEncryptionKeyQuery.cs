using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Workspaces.Members.GrantEncryptionAccess;

/// <summary>
/// Inserts or replaces an ephemeral wrap of a Workspace DEK in
/// <c>ewek_ephemeral_workspace_encryption_keys</c>. The wrap is produced by
/// <see cref="Core.Encryption.InvitationCodeDekWrap"/> at invite-time for brand-new
/// invitees whose public key does not yet exist; the invitee unwraps it during
/// encryption-password setup and the row is promoted to <c>wek_*</c> or deleted.
///
/// The paired cleanup queue job enforces the owner-chosen TTL (24h/48h/7d/30d) by
/// deleting all ephemeral rows for this (workspace, user) pair at
/// <paramref name="expiresAt"/>. If the invitee promotes earlier, the job runs
/// against an empty set and still succeeds.
/// </summary>
public class UpsertEphemeralWorkspaceEncryptionKeyQuery(
    IClock clock)
{
    public void ExecuteTransaction(
        SqliteWriteContext dbWriteContext,
        int workspaceId,
        int userId,
        int storageDekVersion,
        byte[] encryptedWorkspaceDek,
        DateTimeOffset expiresAt,
        int? createdByUserId,
        SqliteTransaction transaction)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     INSERT INTO ewek_ephemeral_workspace_encryption_keys(
                         ewek_workspace_id,
                         ewek_user_id,
                         ewek_storage_dek_version,
                         ewek_encrypted_workspace_dek,
                         ewek_created_at,
                         ewek_expires_at,
                         ewek_created_by_user_id
                     ) VALUES (
                         $workspaceId,
                         $userId,
                         $version,
                         $encryptedWorkspaceDek,
                         $createdAt,
                         $expiresAt,
                         $createdByUserId
                     )
                     ON CONFLICT(ewek_workspace_id, ewek_user_id, ewek_storage_dek_version) DO UPDATE SET
                         ewek_encrypted_workspace_dek = excluded.ewek_encrypted_workspace_dek,
                         ewek_created_at = excluded.ewek_created_at,
                         ewek_expires_at = excluded.ewek_expires_at,
                         ewek_created_by_user_id = excluded.ewek_created_by_user_id
                     RETURNING ewek_user_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .WithParameter("$userId", userId)
            .WithParameter("$version", storageDekVersion)
            .WithParameter("$encryptedWorkspaceDek", encryptedWorkspaceDek)
            .WithParameter("$createdAt", clock.UtcNow)
            .WithParameter("$expiresAt", expiresAt)
            .WithParameter("$createdByUserId", (object?)createdByUserId ?? DBNull.Value)
            .Execute();

        if (result.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Failed to upsert ephemeral workspace encryption key for User '{userId}' on Workspace '{workspaceId}' v{storageDekVersion}.");
        }

        Log.Information(
            "Workspace#{WorkspaceId} ephemeral encryption key v{Version} was staged for User#{UserId} (by User#{CreatedByUserId}, expires at {ExpiresAt:o}).",
            workspaceId, storageDekVersion, userId, createdByUserId, expiresAt);
    }
}
