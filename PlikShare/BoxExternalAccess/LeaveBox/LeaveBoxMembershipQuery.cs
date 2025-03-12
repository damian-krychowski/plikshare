using PlikShare.Boxes.Cache;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Emails;
using PlikShare.Core.Emails.Definitions;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.BoxExternalAccess.LeaveBox;

public class LeaveBoxMembershipQuery(
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
            var leaveBoxResult = dbWriteContext
                .OneRowCmd(
                    sql: """
                         DELETE FROM bm_box_membership
                         WHERE
                             bm_box_id = $boxId
                             AND bm_member_id = $memberId
                             AND bm_was_invitation_accepted = TRUE
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
            
            if (leaveBoxResult.IsEmpty)
            {
                transaction.Rollback();
                
                Log.Warning("Could not leave Box '{BoxExternalId}' membership by Member '{MemberId}' because membership was not found.",
                    boxMembership.Box.ExternalId,
                    boxMembership.Member.Id);

                return ResultCode.BoxMembershipNotFound;
            }
            
            var enqueuedJob = queue.EnqueueOrThrow(
                jobType: EmailQueueJobType.Value,
                definition: new EmailQueueJobDefinition<BoxMemberLeftEmailDefinition>
                {
                    Definition = new BoxMemberLeftEmailDefinition(
                        BoxName: boxMembership.Box.Name,
                        MemberEmail: boxMembership.Member.Email.Value),
                    Email = boxMembership.Box.Workspace.Owner.Email.Value,
                    Template = EmailTemplate.BoxMemberLeft
                },
                correlationId: correlationId,
                executeAfterDate: clock.UtcNow,
                debounceId: null,
                sagaId: null,
                dbWriteContext: dbWriteContext,
                transaction: transaction);
            
            transaction.Commit();
            
            Log.Warning("Box '{BoxExternalId}' membership was left by Member '{MemberId}'. " +
                        "QueryResult: {@QueryResult}",
                boxMembership.Box.ExternalId,
                boxMembership.Member.Id,
                new
                {
                    leaveBoxResult,
                    enqueuedJob
                });

            return ResultCode.Ok;
        }
        catch (Exception e)
        {
            transaction.Rollback();
            
            Log.Error(e, "Something went wrong while leaving Box '{BoxExternalId}' membership by Member '{MemberId}'.",
                boxMembership.Box.ExternalId,
                boxMembership.Member.Id);
            
            throw;
        }
    }
    
    public enum ResultCode
    {
        Ok = 0,
        BoxMembershipNotFound
    }

    private readonly record struct BoxMembership(
        int BoxId,
        int MemberId);
}