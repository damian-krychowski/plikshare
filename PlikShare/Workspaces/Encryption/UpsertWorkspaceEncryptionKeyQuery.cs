using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Workspaces.Encryption;

/// <summary>
/// Inserts or replaces a single user's wrap of a Workspace DEK at a specific Storage DEK
/// version in <c>wek_workspace_encryption_keys</c>. Conflict key is
/// (workspace_id, user_id, storage_dek_version); rotation inserts a new row per (user,
/// version) pair, granting access inserts rows for whichever versions the new member
/// should be able to read.
/// </summary>
public class UpsertWorkspaceEncryptionKeyQuery(
    DbWriteQueue dbWriteQueue,
    IClock clock)
{
    public Task Execute(
        int workspaceId,
        int userId,
        int storageDekVersion,
        byte[] wrappedWorkspaceDek,
        int? wrappedByUserId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspaceId: workspaceId,
                userId: userId,
                storageDekVersion: storageDekVersion,
                wrappedWorkspaceDek: wrappedWorkspaceDek,
                wrappedByUserId: wrappedByUserId),
            cancellationToken: cancellationToken);
    }

    public void ExecuteTransaction(
        SqliteWriteContext dbWriteContext,
        int workspaceId,
        int userId,
        int storageDekVersion,
        byte[] wrappedWorkspaceDek,
        int? wrappedByUserId,
        Microsoft.Data.Sqlite.SqliteTransaction transaction)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     INSERT INTO wek_workspace_encryption_keys(
                         wek_workspace_id,
                         wek_user_id,
                         wek_storage_dek_version,
                         wek_wrapped_workspace_dek,
                         wek_wrapped_at,
                         wek_wrapped_by_user_id
                     ) VALUES (
                         $workspaceId,
                         $userId,
                         $version,
                         $wrappedWorkspaceDek,
                         $wrappedAt,
                         $wrappedByUserId
                     )
                     ON CONFLICT(wek_workspace_id, wek_user_id, wek_storage_dek_version) DO UPDATE SET
                         wek_wrapped_workspace_dek = excluded.wek_wrapped_workspace_dek,
                         wek_wrapped_at = excluded.wek_wrapped_at,
                         wek_wrapped_by_user_id = excluded.wek_wrapped_by_user_id
                     RETURNING wek_user_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .WithParameter("$userId", userId)
            .WithParameter("$version", storageDekVersion)
            .WithParameter("$wrappedWorkspaceDek", wrappedWorkspaceDek)
            .WithParameter("$wrappedAt", clock.UtcNow)
            .WithParameter("$wrappedByUserId", (object?)wrappedByUserId ?? DBNull.Value)
            .Execute();

        if (result.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Failed to upsert workspace encryption key for User '{userId}' on Workspace '{workspaceId}' v{storageDekVersion}.");
        }

        Log.Information(
            "Workspace#{WorkspaceId} encryption key v{Version} was wrapped for User#{UserId} (by User#{WrappedByUserId}).",
            workspaceId, storageDekVersion, userId, wrappedByUserId);
    }

    private void ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        int workspaceId,
        int userId,
        int storageDekVersion,
        byte[] wrappedWorkspaceDek,
        int? wrappedByUserId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            ExecuteTransaction(
                dbWriteContext: dbWriteContext,
                workspaceId: workspaceId,
                userId: userId,
                storageDekVersion: storageDekVersion,
                wrappedWorkspaceDek: wrappedWorkspaceDek,
                wrappedByUserId: wrappedByUserId,
                transaction: transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
