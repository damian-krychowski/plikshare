using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Emails;
using PlikShare.Core.Emails.Definitions;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Users.Cache;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Workspaces.Members.Revoke;

public class RevokeWorkspaceMemberQuery(
    IClock clock,
    IQueue queue,
    DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        WorkspaceContext workspace,
        UserContext member,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace,
                member,
                correlationId),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        WorkspaceContext workspace,
        UserContext member,
        Guid correlationId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();
        try
        {
            var deleteWorkspaceMembershipResult = dbWriteContext
                .OneRowCmd(
                    sql: """
                         DELETE
                         FROM wm_workspace_membership
                         WHERE
                             wm_workspace_id = $workspaceId                          
                             AND wm_member_id = $memberId 
                         RETURNING
                             wm_workspace_id,
                             wm_member_id
                         """,
                    readRowFunc: reader => new DeletedWorkspaceMembership(
                        WorkspaceId: reader.GetInt32(0),
                        MemberId: reader.GetInt32(1)),
                    transaction: transaction)
                .WithParameter("$workspaceId", workspace.Id)
                .WithParameter("$memberId", member.Id)
                .Execute();

            if (deleteWorkspaceMembershipResult.IsEmpty)
            {
                Log.Warning("Could not revoke Workspace '{WorkspaceId}' membership for Member '{MemberExternalId}' because membership was not found.",
                    workspace.Id,
                    member.ExternalId);

                return ResultCode.MembershipNotFound;
            }

            var queueJob = queue.Enqueue(
                correlationId: correlationId,
                jobType: EmailQueueJobType.Value,
                definition: new EmailQueueJobDefinition<WorkspaceMembershipRevokedEmailDefinition>
                {
                    Email = member.Email.Value,
                    Template = EmailTemplate.WorkspaceMembershipRevoked,
                    Definition = new WorkspaceMembershipRevokedEmailDefinition
                    {
                        WorkspaceName = workspace.Name
                    }
                },
                debounceId: null,
                sagaId: null,
                executeAfterDate: clock.UtcNow,
                dbWriteContext: dbWriteContext,
                transaction: transaction);

            transaction.Commit();
            Log.Information("Member '{MemberExternalId}' was revoked from Workspace '{WorkspaceId}'. " +
                           "Query result: '{@QueryResult}'",
                member.ExternalId,
                workspace.Id,
                new
                {
                    MemberId = deleteWorkspaceMembershipResult.Value.MemberId,
                    WorkspaceId = deleteWorkspaceMembershipResult.Value.WorkspaceId,
                    EnqueuedJobId = queueJob.Value.Value
                });

            return ResultCode.Ok;
        }
        catch (Exception e)
        {
            transaction.Rollback();
            Log.Error(e, "Something went wrong while revoking Member '{MemberExternalId}' from Workspace '{WorkspaceId}'",
                member.ExternalId,
                workspace.Id);

            throw;
        }
    }

    public enum ResultCode
    {
        Ok = 0,
        MembershipNotFound
    }

    private readonly record struct DeletedWorkspaceMembership(
        int WorkspaceId,
        int MemberId);
}