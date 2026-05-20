using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Trash;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Workspaces.UpdateTrashPolicy;

public class UpdateWorkspaceTrashPolicyQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        WorkspaceContext workspace,
        TrashPolicy policy,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace: workspace,
                policy: policy),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        WorkspaceContext workspace,
        TrashPolicy policy)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE w_workspaces
                     SET w_trash_policy_json = $policy
                     WHERE w_id = $workspaceId
                     RETURNING w_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithJsonParameter("$policy", policy)
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();

        if (result.IsEmpty)
        {
            Log.Warning("Could not update Workspace#{WorkspaceId} trash policy because Workspace was not found.",
                workspace.Id);

            return ResultCode.NotFound;
        }

        Log.Information("Workspace#{WorkspaceId} trash policy was updated to '{Policy}'",
            workspace.Id,
            Json.Serialize(policy));

        return ResultCode.Ok;
    }

    public enum ResultCode
    {
        Ok = 0,
        NotFound
    }
}
