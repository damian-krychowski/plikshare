using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Emails;
using PlikShare.Core.Emails.Definitions;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Workspaces.Members.RejectInvitation;

public class RejectWorkspaceInvitationQuery(
    IClock clock,
    IQueue queue,
    DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        WorkspaceMembershipContext workspaceMembership,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspaceMembership,
                correlationId),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        WorkspaceMembershipContext workspaceMembership,
        Guid correlationId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var deleteMembershipResult = dbWriteContext
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
                    readRowFunc: reader => new DeletedMembership(
                        WorkspaceId: reader.GetInt32(0),
                        MemberId: reader.GetInt32(1)),
                    transaction: transaction
                )
                .WithParameter("$workspaceId", workspaceMembership.Workspace.Id)
                .WithParameter("$memberId", workspaceMembership.User.Id)
                .Execute();

            if (deleteMembershipResult.IsEmpty)
            {
                transaction.Rollback();

                Log.Warning("Could not reject Workspace '{WorkspaceExternalId}' invitation for Member '{MemberId}' because invitation was not found.",
                    workspaceMembership.Workspace.ExternalId,
                    workspaceMembership.User.Id);

                return ResultCode.MembershipNotFound;
            }

            QueueJobId? enqueuedJobId = null;

            if (workspaceMembership.Invitation?.Inviter is not null)
            {
                var queueJob = queue.Enqueue(
                    correlationId: correlationId,
                    jobType: EmailQueueJobType.Value,
                    definition: new EmailQueueJobDefinition<WorkspaceMembershipInvitationRejectedEmailDefinition>
                    {
                        Email = workspaceMembership.Invitation.Inviter.Email.Value,
                        Template = EmailTemplate.WorkspaceMembershipInvitationRejected,
                        Definition = new WorkspaceMembershipInvitationRejectedEmailDefinition
                        {
                            MemberEmail = workspaceMembership.User.Email.Value,
                            WorkspaceName = workspaceMembership.Workspace.Name
                        }
                    },
                    debounceId: null,
                    sagaId: null,
                    executeAfterDate: clock.UtcNow,
                    dbWriteContext: dbWriteContext,
                    transaction: transaction);

                enqueuedJobId = queueJob.Value;
            }

            transaction.Commit();

            Log.Information("Member '{MemberId}' rejected an invitation to Workspace '{WorkspaceExternalId}'. " +
                            "Query result: '{@QueryResult}'",
                workspaceMembership.User.Id,
                workspaceMembership.Workspace.ExternalId,
            new
            {
                MemberId = deleteMembershipResult.Value.MemberId,
                WorkspaceId = deleteMembershipResult.Value.WorkspaceId,
                EnqueuedJobId = enqueuedJobId
            });

            return ResultCode.Ok;
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while Member '{MemberId}' was rejecting an invitation to Workspace '{WorkspaceExternalId}'",
                workspaceMembership.User.Id,
                workspaceMembership.Workspace.ExternalId);

            throw;
        }
    }

    public enum ResultCode
    {
        Ok = 0,
        MembershipNotFound
    }

    private readonly record struct DeletedMembership(
        int WorkspaceId,
        int MemberId);
}