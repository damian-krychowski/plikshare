using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Workspaces.Encryption;

/// <summary>
/// Removes a user's wrap of a Workspace DEK, revoking their encrypted-workspace access.
/// The DEK itself is unchanged — other members keep their wraps. Forward secrecy against a
/// member who already leaked a plaintext copy of the DEK needs a separate workspace-key
/// rotation flow; this query only revokes the caller's current access.
/// </summary>
public class RevokeWorkspaceEncryptionKeyQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        int workspaceId,
        int userId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspaceId: workspaceId,
                userId: userId),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        int workspaceId,
        int userId)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     DELETE FROM wek_workspace_encryption_keys
                     WHERE wek_workspace_id = $workspaceId
                       AND wek_user_id = $userId
                     RETURNING wek_user_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$workspaceId", workspaceId)
            .WithParameter("$userId", userId)
            .Execute();

        if (result.IsEmpty)
        {
            Log.Warning(
                "Revoke workspace encryption key skipped — User#{UserId} has no wrap on Workspace#{WorkspaceId}.",
                userId, workspaceId);
            return ResultCode.NotFound;
        }

        Log.Information(
            "Workspace#{WorkspaceId} encryption key revoked from User#{UserId}.",
            workspaceId, userId);
        return ResultCode.Ok;
    }

    public enum ResultCode
    {
        Ok = 0,
        NotFound
    }
}
