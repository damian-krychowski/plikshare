using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.UpdateMaxTeamMembers.Contracts;
using Serilog;

namespace PlikShare.Workspaces.UpdateMaxTeamMembers;

public class UpdateWorkspaceMaxTeamMembersQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        WorkspaceContext workspace,
        UpdateWorkspaceMaxTeamMembersRequestDto request,
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
        UpdateWorkspaceMaxTeamMembersRequestDto request)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE w_workspaces
                     SET w_max_team_members = $maxTeamMembers
                     WHERE w_id = $workspaceId
                     RETURNING w_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$maxTeamMembers", request.MaxTeamMembers)
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();

        if (result.IsEmpty)
        {
            Log.Warning("Could not update Workspace#{WorkspaceId} max team members to '{MaxTeamMembers}' because Workspace was not found.",
                workspace.Id,
                request.MaxTeamMembers?.ToString() ?? "NULL");

            return ResultCode.NotFound;
        }
        
        Log.Information("Workspace#{WorkspaceId} max team members was updated to '{MaxTeamMembers}'",
            workspace.Id,
            request.MaxTeamMembers?.ToString() ?? "NULL");

        return ResultCode.Ok;
    } 
    
    public enum ResultCode
    {
        Ok = 0,
        NotFound
    }
}