using PlikShare.Boxes.Cache;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Emails;
using PlikShare.Core.Emails.Definitions;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Boxes.Members.Revoke;

public class RevokeBoxMemberQuery(
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
            var deletedMembershipResult = dbWriteContext
                .OneRowCmd(
                    sql: """
                         DELETE
                         FROM bm_box_membership
                         WHERE
                             bm_box_id = $boxId                          
                             AND bm_member_id = $memberId 
                         RETURNING
                             bm_box_id,
                             bm_member_id
                         """,
                    readRowFunc: reader => new DeletedBoxMembership(
                        BoxId: reader.GetInt32(0),
                        MemberId: reader.GetInt32(1)),
                    transaction: transaction)
                .WithParameter("$boxId", boxMembership.Box.Id)
                .WithParameter("$memberId", boxMembership.Member.Id)
                .Execute();
            
            if (deletedMembershipResult.IsEmpty)
            {
                transaction.Rollback();

                Log.Warning("Could not revoke Box '{BoxExternalId}' membership for Member '{MemberExternalId}' because membership was not found.",
                    boxMembership.Box.ExternalId,
                    boxMembership.Member.ExternalId);

                return ResultCode.MembershipNotFound;
            }
            
            var queueJob = queue.Enqueue(
                correlationId: correlationId,
                jobType: EmailQueueJobType.Value,
                definition: new EmailQueueJobDefinition<BoxMembershipRevokedEmailDefinition>
                {
                    Email = boxMembership.Member.Email.Value,
                    Definition = new BoxMembershipRevokedEmailDefinition
                    {
                        BoxName = boxMembership.Box.Name,
                    },
                    Template = EmailTemplate.BoxMembershipRevoked
                },
                debounceId: null,
                sagaId: null,
                executeAfterDate: clock.UtcNow,
                dbWriteContext: dbWriteContext,
                transaction: transaction);
            
            transaction.Commit();

            Log.Information("Member '{MemberExternalId}' was revoked from Box '{BoxExternalId}'. " +
                            "Query result: '{@QueryResult}'",
                boxMembership.Member.ExternalId,
                boxMembership.Box.ExternalId,
                new {
                    deletedMembershipResult,
                    queueJob
                });

            return ResultCode.Ok;
        }
        catch (Exception e)
        {
            transaction.Rollback();
            
            Log.Error(e,
                "Something went wrong while revoking membership for Box '{BoxExternalId}' and Member '{MemberExternalId}'.",
                boxMembership.Box.ExternalId,
                boxMembership.Member.ExternalId);
            
            throw;
        }
    }
    
    public enum ResultCode
    {
        Ok = 0,
        MembershipNotFound
    }

    private readonly record struct DeletedBoxMembership(
        int BoxId,
        int MemberId);
}