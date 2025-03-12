using PlikShare.Boxes.Cache;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Emails;
using PlikShare.Core.Emails.Definitions;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.BoxExternalAccess.Invitations.Reject;

public class RejectBoxInvitationQuery(
    DbWriteQueue dbWriteQueue,
    IClock clock,
    IQueue queue)
{
    public Task<ResultCode> Execute(
        BoxMembershipContext boxMembership,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                boxMembership: boxMembership,
                correlationId: correlationId),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        BoxMembershipContext boxMembership,
        Guid correlationId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();
        
        try
        {
            var rejectedBoxMembership = dbWriteContext
                .OneRowCmd(
                    sql: """
                         DELETE FROM bm_box_membership
                         WHERE
                             bm_box_id = $boxId
                             AND bm_member_id = $memberId
                             AND bm_was_invitation_accepted = FALSE
                         RETURNING 
                             bm_box_id,
                             bm_member_id
                         """,
                    readRowFunc: reader => new BoxMembership(
                        BoxId: reader.GetInt32(0),
                        MemberId: reader.GetInt32(1)),
                    transaction: transaction)
                .WithParameter("$boxId", boxMembership.Box.Id)
                .WithParameter("$memberId", boxMembership.Member.Id)
                .Execute();
                
            if (rejectedBoxMembership.IsEmpty)
            {
                transaction.Rollback();

                Log.Warning("Could not reject Box '{BoxExternalId}' invitation for Member '{MemberId}' because invitation was not found.",
                    boxMembership.Box.ExternalId,
                    boxMembership.Member.Id);

                return ResultCode.BoxInvitationNotFound;
            }

            QueueJobId? enqueuedJob = null;

            if (boxMembership.Inviter is not null)
            {
                enqueuedJob = queue.EnqueueOrThrow(
                    jobType: EmailQueueJobType.Value,
                    definition: new EmailQueueJobDefinition<BoxMembershipInvitationRejectedEmailDefinition>
                    {
                        Definition = new BoxMembershipInvitationRejectedEmailDefinition(
                            BoxName: boxMembership.Box.Name,
                            InviteeEmail: boxMembership.Member.Email.Value),
                        Email = boxMembership.Inviter.Email.Value,
                        Template = EmailTemplate.BoxMembershipInvitationRejected
                    },
                    correlationId: correlationId,
                    executeAfterDate: clock.UtcNow,
                    debounceId: null,
                    sagaId: null,
                    dbWriteContext: dbWriteContext,
                    transaction: transaction);
            }
            
            transaction.Commit();
            
            Log.Information("Box '{BoxExternalId}' invitation for Member '{MemberId}' was rejected. " +
                            "QueryResult: {@QueryResult}",
                boxMembership.Box.ExternalId,
                boxMembership.Member.Id,
                new
                {
                    rejectedBoxMembership,
                    enqueuedJob
                });

            return ResultCode.Ok;
        }
        catch (Exception e)
        {
            transaction.Rollback();
            
            Log.Error(e, "Something went wrong while rejecting Box '{BoxExternalId}' invitation for Member '{MemberId}'.",
                boxMembership.Box.ExternalId,
                boxMembership.Member.Id);
            
            throw;
        }
    }
    
    public enum ResultCode
    {
        Ok = 0,
        BoxInvitationNotFound
    }

    private readonly record struct BoxMembership(
        int BoxId,
        int MemberId);
}