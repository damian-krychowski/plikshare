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
    IQueue queue)
{
    /// <summary>
    /// Inserts memberships and enqueues invitation emails inside the caller's transaction.
    /// Returns IDs of members that were actually inserted (i.e. not duplicates) — the caller
    /// needs this to compose follow-up writes (e.g. auto-grant of wek wraps) only for new
    /// memberships, all atomically with the invitation insert.
    /// </summary>
    public int[] ExecuteTransaction(
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction,
        WorkspaceContext workspace,
        UserContext inviter,
        List<Member> members,
        bool allowShare,
        Guid correlationId)
    {
        var invitedMemberIds = new List<int>();
        var createdQueueJobIds = new List<QueueJobId>();

        foreach (var member in members)
        {
            var insertInvitationResult = InsertWorkspaceInvitation(
                member.User.Id,
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

            invitedMemberIds.Add(member.User.Id);
            createdQueueJobIds.Add(queueJob.Value);
        }

        Log.Information("Members '{MemberIds}' was invited to Workspace#{WorkspaceId} by Inviter '{InviterId}'. " +
                        "QueryResult: '{@QueryResult}'",
            members.Select(x => x.User.Id),
            workspace.Id,
            inviter.Id,
            new
            {
                InvitedMembers = invitedMemberIds,
                CreatedQueueJobs = createdQueueJobIds
            });

        return invitedMemberIds.ToArray();
    }

    private SQLiteOneRowCommandResult<QueueJobId> EnqueueWorkspaceInvitationEmail(
        Guid correlationId,
        Member member,
        Email inviterEmail,
        string workspaceName,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        return queue.Enqueue(
            correlationId: correlationId,
            jobType: EmailQueueJobType.Value,
            definition: new EmailQueueJobDefinition<WorkspaceMembershipInvitationEmailDefinition>
            {
                Email = member.User.Email.Value,
                Template = EmailTemplate.WorkspaceMembershipInvitation,
                Definition = new WorkspaceMembershipInvitationEmailDefinition(
                    InviterEmail: inviterEmail.Value,
                    WorkspaceName: workspaceName,
                    InvitationCode: member.InvitationCode?.Value)
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
        SqliteWriteContext dbWriteContext,
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

    public record Member(
        UserContext User,
        InvitationCode? InvitationCode);
}
