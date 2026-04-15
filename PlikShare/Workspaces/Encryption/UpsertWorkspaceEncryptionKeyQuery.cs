using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Workspaces.Encryption;

/// <summary>
/// Inserts or replaces a single user's wrap of a Workspace DEK in
/// <c>wek_workspace_encryption_keys</c>. Used for the owner's wrap at workspace creation,
/// for rewrapping the DEK onto a newly-invited team member, and for re-wrapping on
/// encryption-password rotation.
/// </summary>
public class UpsertWorkspaceEncryptionKeyQuery(
    DbWriteQueue dbWriteQueue,
    IClock clock)
{
    public Task Execute(
        int workspaceId,
        int userId,
        byte[] wrappedWorkspaceDek,
        int? wrappedByUserId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspaceId: workspaceId,
                userId: userId,
                wrappedWorkspaceDek: wrappedWorkspaceDek,
                wrappedByUserId: wrappedByUserId),
            cancellationToken: cancellationToken);
    }

    public void ExecuteTransaction(
        SqliteWriteContext dbWriteContext,
        int workspaceId,
        int userId,
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
                         wek_wrapped_workspace_dek,
                         wek_wrapped_at,
                         wek_wrapped_by_user_id
                     ) VALUES (
                         $workspaceId,
                         $userId,
                         $wrappedWorkspaceDek,
                         $wrappedAt,
                         $wrappedByUserId
                     )
                     ON CONFLICT(wek_workspace_id, wek_user_id) DO UPDATE SET
                         wek_wrapped_workspace_dek = excluded.wek_wrapped_workspace_dek,
                         wek_wrapped_at = excluded.wek_wrapped_at,
                         wek_wrapped_by_user_id = excluded.wek_wrapped_by_user_id
                     RETURNING wek_user_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .WithParameter("$userId", userId)
            .WithParameter("$wrappedWorkspaceDek", wrappedWorkspaceDek)
            .WithParameter("$wrappedAt", clock.UtcNow)
            .WithParameter("$wrappedByUserId", (object?)wrappedByUserId ?? DBNull.Value)
            .Execute();

        if (result.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Failed to upsert workspace encryption key for User '{userId}' on Workspace '{workspaceId}'.");
        }

        Log.Information(
            "Workspace#{WorkspaceId} encryption key was wrapped for User#{UserId} (by User#{WrappedByUserId}).",
            workspaceId, userId, wrappedByUserId);
    }

    private void ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        int workspaceId,
        int userId,
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
