using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Emails;
using PlikShare.Core.Emails.Definitions;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Users.Cache;
using PlikShare.Users.Entities;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Workspaces.Members.CreateInvitation;

public class CreateWorkspaceMemberInvitationQuery(
    IClock clock,
    IQueue queue,
    DbWriteQueue dbWriteQueue)
{
    public Task Execute(
        WorkspaceContext workspace,
        UserContext inviter,
        UserContext[] members,
        bool allowShare,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace,
                inviter,
                members,
                allowShare,
                correlationId),
            cancellationToken: cancellationToken);
    }

    private void ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        WorkspaceContext workspace,
        UserContext inviter,
        UserContext[] members,
        bool allowShare,
        Guid correlationId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var invitedMemberIds = new List<int>();
            var createdQueueJobIds = new List<QueueJobId>();

            foreach (var member in members)
            {
                var insertInvitationResult = InsertWorkspaceInvitation(
                    member.Id,
                    workspace.Id,
                    inviter.Id,
                    allowShare,
                    dbWriteContext,
                    transaction);

                if (insertInvitationResult.IsEmpty)
                    continue; //member with given id was already invited to that workspace so we ignore and continue

                var queueJob = EnqueueWorkspaceInvitationEmail(
                    correlationId,
                    member,
                    inviter.Email,
                    workspace.Name,
                    dbWriteContext,
                    transaction);

                invitedMemberIds.Add(member.Id);
                createdQueueJobIds.Add(queueJob.Value);
            }

            transaction.Commit();

            Log.Information("Members '{MemberIds}' was invited to Workspace#{WorkspaceId} by Inviter '{InviterId}'. " +
                            "QueryResult: '{@QueryResult}'",
                members.Select(x => x.Id),
                workspace.Id,
                inviter.Id,
                new
                {
                    InvitedMembers = invitedMemberIds,
                    CreatedQueueJobs = createdQueueJobIds
                });
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e,
                "Something went wrong while creating Workspace#{WorkspaceId} invitation by Inviter '{InviterId}' to Member '{MemberIds}'",
                workspace.Id,
                inviter.Id,
                members.Select(x => x.Id));

            throw;
        }
    }

    private SQLiteOneRowCommandResult<QueueJobId> EnqueueWorkspaceInvitationEmail(
        Guid correlationId,
        UserContext member,
        Email inviterEmail,
        string workspaceName,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        var wasMemberFreshlyInvited = member.Status == UserStatus.Invitation;

        return queue.Enqueue(
            correlationId: correlationId,
            jobType: EmailQueueJobType.Value,
            definition: new EmailQueueJobDefinition<WorkspaceMembershipInvitationEmailDefinition>
            {
                Email = member.Email.Value,
                Template = EmailTemplate.WorkspaceMembershipInvitation,
                Definition = new WorkspaceMembershipInvitationEmailDefinition(
                    InviterEmail: inviterEmail.Value,
                    WorkspaceName: workspaceName,
                    InvitationCode: wasMemberFreshlyInvited
                        ? member.Invitation!.Code
                        : null)
            },
            executeAfterDate: clock.UtcNow,
            debounceId: null,
            sagaId: null,
            dbWriteContext: dbWriteContext,
            transaction: transaction);
    }

    private SQLiteOneRowCommandResult<int> InsertWorkspaceInvitation(
        int memberId,
        int workspaceId,
        int inviterId,
        bool allowShare,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        return dbWriteContext
            .OneRowCmd(
                sql: """
                     INSERT INTO wm_workspace_membership (
                         wm_workspace_id,
                         wm_member_id,
                         wm_inviter_id,
                         wm_was_invitation_accepted,
                         wm_allow_share,
                         wm_created_at
                     ) VALUES (
                         $workspaceId,
                         $memberId,
                         $inviterId,
                         FALSE,
                         $allowShare,
                         $now
                     )                                    
                     ON CONFLICT(wm_workspace_id, wm_member_id) DO NOTHING
                     RETURNING wm_member_id                        
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .WithParameter("$memberId", memberId)
            .WithParameter("$inviterId", inviterId)
            .WithParameter("$allowShare", allowShare)
            .WithParameter("$now", clock.UtcNow)
            .Execute();
    }
}