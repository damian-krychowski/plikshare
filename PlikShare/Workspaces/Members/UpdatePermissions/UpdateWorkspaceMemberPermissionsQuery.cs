using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Users.Cache;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Permissions;
using Serilog;

namespace PlikShare.Workspaces.Members.UpdatePermissions;

public class UpdateWorkspaceMemberPermissionsQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        WorkspaceContext workspace,
        UserContext member,
        WorkspacePermissions permissions,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace: workspace,
                member: member,
                permissions: permissions),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        WorkspaceContext workspace,
        UserContext member,
        WorkspacePermissions permissions)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();
        
        try
        {
            var updateWorkspaceMembershipResult = dbWriteContext
                .OneRowCmd(
                    sql: """
                         UPDATE wm_workspace_membership
                         SET 
                             allow_share = $allowShare
                         WHERE 
                             wm_workspace_id = $workspaceId
                             AND wm_member_id = $memberId
                         RETURNING 
                             wm_workspace_id,
                             wm_member_id
                         """,
                    readRowFunc: reader => new UpdatedWorkspaceMembership(
                        WorkspaceId: reader.GetInt32(0),
                        MemberId: reader.GetInt32(1)),
                    transaction: transaction)
                .WithParameter("$allowShare", permissions.AllowShare)
                .WithParameter("$workspaceId", workspace.Id)
                .WithParameter("$memberId", member.Id)
                .Execute();
            
            if (updateWorkspaceMembershipResult.IsEmpty)
            {
                transaction.Rollback();
                
                Log.Warning("Could not update Workspace '{WorkspaceId}' membership permissions for Member '{MemberExternalId}' to '{@Permissions} because membership was not found'",
                    workspace.Id,
                    member.ExternalId,
                    permissions);

                return ResultCode.NotFound;
            }
            
            transaction.Commit();

            Log.Information(
                "Workspace '{WorkspaceId}' permissions for Member '{MemberExternalId} ({MemberId})' were updated to '{@Permissions}'",
                workspace.Id,
                member.ExternalId,
                member.Id,
                permissions);

            return ResultCode.Ok;
        }
        catch (Exception e)
        {
            transaction.Rollback();
            
            Log.Error(e, "Something went wrong while updating Member '{MemberId}' of Workspace '{WorkspaceId}' permissions to '{@Permissions}'",
                member.Id,
                workspace.Id,
                permissions);
            
            throw;
        }
    }
    
    public enum ResultCode
    {
        Ok = 0,
        NotFound
    }
    
    private readonly record struct UpdatedWorkspaceMembership(
        int WorkspaceId,
        int MemberId);
}