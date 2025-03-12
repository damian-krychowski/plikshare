using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Workspaces.UpdateName;

public class UpdateWorkspaceNameQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        WorkspaceContext workspace,
        string name,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace,
                name),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        WorkspaceContext workspace,
        string name)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE w_workspaces
                     SET w_name = $name
                     WHERE 
                         w_id = $workspaceId
                         AND w_is_being_deleted = FALSE
                     RETURNING w_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$name", name)
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();

        if (result.IsEmpty)
        {
            Log.Warning("Could not update Workspace '{WorkspaceId}' name to '{Name}' because Workspace was not found.",
                workspace.Id,
                name);

            return ResultCode.NotFound;
        }
        
        Log.Information("Workspace '{WorkspaceId}' name was updated to '{Name}'",
            workspace.Id,
            name);

        return ResultCode.Ok;
    } 
    
    public enum ResultCode
    {
        Ok = 0,
        NotFound
    }
}