using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Emails;
using PlikShare.Core.Emails.Definitions;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Storages.Encryption;
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
    /// Inserts memberships inside the caller's transaction and decides per-workspace how
    /// the invitation email is delivered:
    ///
    /// - For non full-encryption workspaces: enqueues a queue job per fresh membership
    ///   inside the same transaction (legacy async path).
    /// - For full-encryption workspaces: DOES NOT enqueue any email job. Surfaces the
    ///   pending invitations in <see cref="Result.PendingSyncEmails"/> so the caller can
    ///   drive synchronous send post-commit and rollback DB state if the SMTP/Resend
    ///   send fails. This keeps plaintext invitation codes — which double as KEKs for
    ///   ephemeral DEK wraps — out of <c>q_queue.q_definition</c> (and after success
    ///   <c>qc_queue_completed.qc_definition</c>) entirely.
    /// </summary>
    public Result ExecuteTransaction(
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction,
        WorkspaceContext workspace,
        UserContext inviter,
        List<Member> members,
        bool allowShare,
        Guid correlationId)
    {
        var isFullEncryption = workspace.EncryptionType == StorageEncryptionType.Full;

        var invitedMemberIds = new HashSet<int>();
        var createdQueueJobIds = new List<QueueJobId>();
        var pendingSyncEmails = new List<PendingSyncEmail>();

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

            if (isFullEncryption)
            {
                pendingSyncEmails.Add(new PendingSyncEmail(
                    MemberId: member.Id,
                    InviteeEmail: member.Email.Value,
                    InvitationCode: member.InvitationCode?.Value));
            }
            else
            {
                var queueJob = EnqueueWorkspaceInvitationEmail(
                    correlationId,
                    member,
                    inviter.Email,
                    workspace.Name,
                    dbWriteContext,
                    transaction);

                createdQueueJobIds.Add(queueJob.Value);
            }

            invitedMemberIds.Add(member.Id);
        }

        Log.Information("Members '{MemberIds}' was invited to Workspace#{WorkspaceId} by Inviter '{InviterId}'. " +
                        "QueryResult: '{@QueryResult}'",
            members.Select(x => x.Id),
            workspace.Id,
            inviter.Id,
            new
            {
                InvitedMembers = invitedMemberIds,
                CreatedQueueJobs = createdQueueJobIds,
                PendingSyncEmailCount = pendingSyncEmails.Count
            });

        return new Result(
            NewlyInvitedMemberIds: invitedMemberIds,
            PendingSyncEmails: pendingSyncEmails);
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
                Email = member.Email.Value,
                Template = EmailTemplate.WorkspaceMembershipInvitation,
                Definition = new WorkspaceMembershipInvitationEmailDefinition(
                    InviterEmail: inviterEmail.Value,
                    WorkspaceName: workspaceName,
                    InvitationCode: member.InvitationCode?.Value)
            },
            executeAfterDate: clock.UtcNow,
            debounceId: null,
            sagaId: null,
            batch: null,
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
        int Id,
        Email Email,
        InvitationCode? InvitationCode);

    public readonly record struct Result(
        HashSet<int> NewlyInvitedMemberIds,
        List<PendingSyncEmail> PendingSyncEmails);

    public readonly record struct PendingSyncEmail(
        int MemberId,
        string InviteeEmail,
        string? InvitationCode);
}
