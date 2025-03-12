using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Users.Cache;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Workspaces.ChangeOwner;

public class ChangeWorkspaceOwnerQuery(DbWriteQueue dbWriteQueue)
{
    public Task Execute(
        WorkspaceContext workspace,
        UserContext newOwner,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace,
                newOwner),
            cancellationToken: cancellationToken);
    }

    private void ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        WorkspaceContext workspace,
        UserContext newOwner)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();
        try
        {
            dbWriteContext
                .OneRowCmd(
                    sql: """
                         UPDATE w_workspaces
                         SET w_owner_id = $newOwnerId
                         WHERE w_id = $workspaceId
                         RETURNING w_id                    
                         """,
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$newOwnerId", newOwner.Id)
                .WithParameter("$workspaceId", workspace.Id)
                .ExecuteOrThrow();

            var membershipDeletion = dbWriteContext
                .OneRowCmd(
                    sql: """
                         DELETE FROM wm_workspace_membership
                         WHERE 
                             wm_workspace_id = $workspaceId
                             AND wm_member_id = $newOwnerId
                         RETURNING
                             wm_workspace_id
                         """,
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$newOwnerId", newOwner.Id)
                .WithParameter("$workspaceId", workspace.Id)
                .Execute();

            transaction.Commit();
            if (membershipDeletion.IsEmpty)
            {
                Log.Information("Workspace '{WorkspaceExternalId}' owner was changed to User '{UserExternalId}'",
                    workspace.ExternalId,
                    newOwner.ExternalId);
            }
            else
            {
                Log.Information("Workspace '{WorkspaceExternalId}' owner was changed to User '{UserExternalId}'. " +
                                "New owner used to be workspace member, but that membership was deleted.",
                    workspace.ExternalId,
                    newOwner.ExternalId);
            }
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while changing owner of Workspace '{WorkspaceExternalId}' to User '{UserExternalId}'",
                workspace.ExternalId,
                newOwner.ExternalId);

            throw;
        }
    }
}