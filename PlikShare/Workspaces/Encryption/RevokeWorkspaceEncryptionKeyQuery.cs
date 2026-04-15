using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Workspaces.Encryption;

/// <summary>
/// Removes ALL wraps of Workspace DEKs owned by this user for this workspace, across every
/// Storage DEK version they held. The DEKs themselves are unchanged — other members keep
/// their wraps. Forward secrecy against a member who already leaked a plaintext copy of a
/// DEK needs a separate rotation flow; this query only revokes current access.
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
        var removedVersions = dbWriteContext
            .Cmd(
                sql: """
                     DELETE FROM wek_workspace_encryption_keys
                     WHERE wek_workspace_id = $workspaceId
                       AND wek_user_id = $userId
                     RETURNING wek_storage_dek_version
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$workspaceId", workspaceId)
            .WithParameter("$userId", userId)
            .Execute();

        if (removedVersions.Count == 0)
        {
            Log.Warning(
                "Revoke workspace encryption key skipped — User#{UserId} has no wrap on Workspace#{WorkspaceId}.",
                userId, workspaceId);
            return ResultCode.NotFound;
        }

        Log.Information(
            "Workspace#{WorkspaceId} encryption key versions [{Versions}] revoked from User#{UserId}.",
            workspaceId, removedVersions, userId);
        return ResultCode.Ok;
    }

    public enum ResultCode
    {
        Ok = 0,
        NotFound
    }
}
