using Microsoft.Data.Sqlite;
using PlikShare.Boxes.Cache;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Emails;
using PlikShare.Core.Emails.Definitions;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Users.Cache;
using PlikShare.Users.Entities;
using Serilog;

namespace PlikShare.Boxes.Members.CreateInvitation;

public class CreateBoxMemberInvitationQuery(
    DbWriteQueue dbWriteQueue,
    IClock clock,
    IQueue queue)
{
    public Task Execute(
        BoxContext box,
        UserContext inviter,
        UserContext[] members,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                box: box,
                inviter: inviter,
                members: members,
                correlationId: correlationId),
            cancellationToken: cancellationToken);
    }

    private void ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        BoxContext box,
        UserContext inviter,
        UserContext[] members,
        Guid correlationId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();
        
        try
        {
            var invitedMemberIds = new List<int>();
            var createdQueueJobIds = new List<int>();

            foreach (var member in members)
            {
                var insertInvitationResult = InsertBoxInvitation(
                    member.Id,
                    box.Id,
                    inviter.Id, 
                    dbWriteContext,
                    transaction);

                if (insertInvitationResult.IsEmpty) 
                    continue; 
                
                var queueJob = EnqueueBoxInvitationEmail(
                    correlationId, 
                    member,
                    inviter.Email, 
                    box.Name, 
                    dbWriteContext, 
                    transaction);

                invitedMemberIds.Add(member.Id);
                createdQueueJobIds.Add(queueJob.Value.Value);
            }
            
            transaction.Commit();

            Log.Information(
                "Box '{BoxExternalId}' invitation for Members '{MemberIds}' by Inviter '{InviterId}' in Workspace#{WorkspaceId} was created. " +
                "QueryResult: {@QueryResult}",
                box.ExternalId,
                members.Select(member => member.Id),
                inviter.Id,
                box.Workspace.Id,
                new
                {
                    invitedMemberIds,
                    createdQueueJobIds
                });
        } 
        catch (Exception e)
        {
            transaction.Rollback();
            
            Log.Error(e, "Something went wrong while creating Box '{BoxExternalId}' invitation for Member '{MemberIds}' by Inviter '{InviterId}' in Workspace#{WorkspaceId}",
                box.ExternalId,
                members.Select(member => member.Id),
                inviter.Id,
                box.Workspace.Id);
            
            throw;
        }
    }
    
    private SQLiteOneRowCommandResult<int> InsertBoxInvitation(
        int memberId,
        int boxId, 
        int inviterId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        return dbWriteContext
            .OneRowCmd(
                sql: """
                     INSERT INTO bm_box_membership (
                         bm_box_id,
                         bm_member_id,
                         bm_inviter_id,
                         bm_was_invitation_accepted,
                         bm_allow_download,
                         bm_allow_upload,
                         bm_allow_list,
                         bm_allow_delete_file,
                         bm_allow_rename_file,
                         bm_allow_move_items,
                         bm_allow_create_folder,
                         bm_allow_delete_folder,
                         bm_allow_rename_folder,
                         bm_created_at
                     ) VALUES (                    
                         $boxId,
                         $memberId,
                         $inviterId,
                         FALSE,
                         FALSE,
                         FALSE,
                         TRUE,
                         FALSE,
                         FALSE,
                         FALSE,
                         FALSE,
                         FALSE,
                         FALSE,
                         $now
                     )
                     ON CONFLICT(bm_box_id, bm_member_id) DO NOTHING
                     RETURNING bm_member_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$boxId", boxId)
            .WithParameter("$memberId", memberId)
            .WithParameter("$inviterId", inviterId)
            .WithParameter("$now", clock.UtcNow)
            .Execute();
    }

    private SQLiteOneRowCommandResult<QueueJobId> EnqueueBoxInvitationEmail(
        Guid correlationId, 
        UserContext member,
        Email inviterEmail, 
        string boxName,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        var wasMemberFreshlyInvited = member.Status == UserStatus.Invitation;
        
        return queue.Enqueue(
            correlationId: correlationId,
            jobType: EmailQueueJobType.Value,
            definition: new EmailQueueJobDefinition<BoxMembershipInvitationEmailDefinition>
            {
                Email = member.Email.Value,
                Template = EmailTemplate.BoxMembershipInvitation,
                Definition = new BoxMembershipInvitationEmailDefinition(
                    InviterEmail: inviterEmail.Value,
                    BoxName: boxName,
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
}