using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Emails;
using PlikShare.Core.Emails.Definitions;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Users.Cache;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Workspaces.Members.LeaveWorkspace;

public class LeaveSharedWorkspaceQuery(
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
                transaction.Rollback();

                Log.Warning("Could not leave Workspace#{WorkspaceId} by Member '{MemberId}' because membership was not found.",
                    workspace.Id,
                    member.Id);

                return ResultCode.MembershipNotFound;
            }

            var queueJob = queue.Enqueue(
                correlationId: correlationId,
                jobType: EmailQueueJobType.Value,
                definition: new EmailQueueJobDefinition<WorkspaceMemberLeftEmailDefinition>
                {
                    Email = workspace.Owner.Email.Value,
                    Template = EmailTemplate.WorkspaceMemberLeft,
                    Definition = new WorkspaceMemberLeftEmailDefinition
                    {
                        WorkspaceName = workspace.Name,
                        MemberEmail = member.Email.Value
                    }
                },
                debounceId: null,
                sagaId: null,
                executeAfterDate: clock.UtcNow,
                dbWriteContext: dbWriteContext,
                transaction: transaction);

            transaction.Commit();

            Log.Information("Member '{MemberId}' left Workspace#{WorkspaceId}. " +
                            "QueryResult: '{@QueryResult}'",
                member.Id,
                workspace.Id,
                new
                {
                    deleteWorkspaceMembershipResult,
                    queueJob
                });

            return ResultCode.Ok;
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while Member '{MemberId}' was leaving Workspace#{WorkspaceId}",
                member.Id,
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