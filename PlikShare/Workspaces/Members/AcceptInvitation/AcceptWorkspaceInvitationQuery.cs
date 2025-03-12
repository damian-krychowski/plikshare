using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Emails;
using PlikShare.Core.Emails.Definitions;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Workspaces.Members.AcceptInvitation;

public class AcceptWorkspaceInvitationQuery(
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
            var updateMembershipResult = dbWriteContext
                .OneRowCmd(
                    sql: """
                         UPDATE wm_workspace_membership
                         SET wm_was_invitation_accepted = TRUE
                         WHERE 
                             wm_workspace_id = $workspaceId
                             AND wm_member_id = $memberId
                         RETURNING 
                             wm_workspace_id,
                             wm_member_id
                         """,
                    readRowFunc: reader => new MembershipInvitationAccepted(
                        WorkspaceId: reader.GetInt32(0),
                        MemberId: reader.GetInt32(1)),
                    transaction: transaction)
                .WithParameter("$workspaceId", workspaceMembership.Workspace.Id)
                .WithParameter("$memberId", workspaceMembership.User.Id)
                .Execute();

            if (updateMembershipResult.IsEmpty)
            {
                transaction.Rollback();

                Log.Warning("Could not accept Workspace '{WorkspaceExternalId}' invitation for Member '{MemberId}' because workspace membership was not found.",
                    workspaceMembership.Workspace.ExternalId,
                    workspaceMembership.User.Id);

                return ResultCode.MembershipNotFound;
            }

            int? enqueuedJobId = null;

            if (workspaceMembership.Invitation?.Inviter is not null)
            {
                var queueJob = queue.Enqueue(
                    correlationId: correlationId,
                    jobType: EmailQueueJobType.Value,
                    definition: new EmailQueueJobDefinition<WorkspaceMembershipInvitationAcceptedEmailDefinition>
                    {
                        Email = workspaceMembership.Invitation.Inviter.Email.Value,
                        Template = EmailTemplate.WorkspaceMembershipInvitationAccepted,
                        Definition = new WorkspaceMembershipInvitationAcceptedEmailDefinition
                        {
                            InviteeEmail = workspaceMembership.User.Email.Value,
                            WorkspaceName = workspaceMembership.Workspace.Name
                        }
                    },
                    debounceId: null,
                    sagaId: null,
                    executeAfterDate: clock.UtcNow,
                    dbWriteContext: dbWriteContext,
                    transaction: transaction);

                enqueuedJobId = queueJob.Value.Value;
            }


            transaction.Commit();

            Log.Information("Workspace '{WorkspaceExternalId}' invitation was accepted by Member '{MemberId}'. " +
                            "Query result: '{@QueryResult}'",
                workspaceMembership.Workspace.ExternalId,
                workspaceMembership.User.Id,
                new
                {
                    MemberId = updateMembershipResult.Value.MemberId,
                    WorkspaceId = updateMembershipResult.Value.WorkspaceId,
                    EnqueuedJobId = enqueuedJobId
                });

            return ResultCode.Ok;
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while accepting Workspace '{WorkspaceExternalId}' invitation by Member '{MemberId}'",
                workspaceMembership.Workspace.ExternalId,
                workspaceMembership.User.Id);

            throw;
        }
    }

    public enum ResultCode
    {
        Ok = 0,
        MembershipNotFound
    }

    private readonly record struct MembershipInvitationAccepted(
        int WorkspaceId,
        int MemberId);
}