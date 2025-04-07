using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.UpdateMaxSize.Contracts;
using Serilog;

namespace PlikShare.Workspaces.UpdateMaxSize;

public class UpdateWorkspaceMaxSizeQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        WorkspaceContext workspace,
        UpdateWorkspaceMaxSizeDto request,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace,
                request),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        WorkspaceContext workspace,
        UpdateWorkspaceMaxSizeDto request)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE w_workspaces
                     SET w_max_size_in_bytes = $maxSizeInBytes
                     WHERE w_id = $workspaceId
                     RETURNING w_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$maxSizeInBytes", request.MaxSizeInBytes)
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();

        if (result.IsEmpty)
        {
            Log.Warning("Could not update Workspace#{WorkspaceId} max size in bytes to '{MaxSizeInByes}' because Workspace was not found.",
                workspace.Id,
                request.MaxSizeInBytes?.ToString() ?? "NULL");

            return ResultCode.NotFound;
        }
        
        Log.Information("Workspace#{WorkspaceId} max size in bytes was updated to '{MaxSizeInByes}'",
            workspace.Id,
            request.MaxSizeInBytes?.ToString() ?? "NULL");

        return ResultCode.Ok;
    } 
    
    public enum ResultCode
    {
        Ok = 0,
        NotFound
    }
}