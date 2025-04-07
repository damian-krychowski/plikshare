using Microsoft.Data.Sqlite;
using PlikShare.Boxes.Id;
using PlikShare.BoxLinks.Id;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Emails;
using PlikShare.Core.Emails.Definitions;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Users.Entities;
using Serilog;

namespace PlikShare.Boxes.Delete.QueueJob;

public class BatchDeleteBoxesWithDependenciesQuery(
    IQueue queue,
    IClock clock)
{
    public Result Execute(
        int workspaceId,
        List<int> boxIds,
        Guid correlationId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        var deletedBoxLinks = DeleteBoxLinks(
            boxIds,
            dbWriteContext,
            transaction);
        
        var deletedBoxMembers = DeleteBoxMembers(
            boxIds, 
            dbWriteContext, 
            transaction);

        var jobsToEnqueue = new List<BulkQueueJobEntity>();

        foreach (var deletedBoxMember in deletedBoxMembers)
        {
            var job = queue.CreateBulkEntity(
                jobType: EmailQueueJobType.Value,
                definition: new EmailQueueJobDefinition<BoxMembershipRevokedEmailDefinition>
                {
                    Email = deletedBoxMember.Email.Value,
                    Definition = new BoxMembershipRevokedEmailDefinition
                    {
                        BoxName = deletedBoxMember.BoxName
                    },
                    Template = EmailTemplate.BoxMembershipRevoked
                },
                sagaId: null);

            jobsToEnqueue.Add(job);
        }

        var queueJobIds = queue.EnqueueBulk(
            correlationId: correlationId,
            definitions: jobsToEnqueue,
            executeAfterDate: clock.UtcNow,
            dbWriteContext: dbWriteContext,
            transaction: transaction);

        var deletedBoxes = DeleteBoxes(
            workspaceId,
            boxIds,
            dbWriteContext,
            transaction);
        
        Log.Information("Boxes '{BoxIds}' in Workspace#{WorkspaceId} delete operation finished. " +
                        "Operation result: {@QueryResult}",
            boxIds,
            workspaceId,
            new
            {
                deletedBoxLinks,
                deletedBoxMembers,
                deletedBoxes,
                queueJobIds
            });

        return new Result(
            DeletedBoxes: deletedBoxes
                .Select(box => box.ExternalId)
                .ToArray(),
            
            DeletedBoxMembers: deletedBoxMembers
                .Select(member => new BoxMember(
                    BoxExternalId: member.BoxExternalId,
                    MemberId: member.MemberId))
                .ToArray());
    }
    
    private static List<DeletedBoxLink> DeleteBoxLinks(
        List<int> boxIds,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        if (boxIds.Count == 0)
            return [];

        return dbWriteContext
            .Cmd(
                sql: @"
                    DELETE FROM bl_box_links
                    WHERE
                        bl_box_id IN (
                            SELECT value FROM json_each($boxIds)
                        )
                    RETURNING 
                        bl_external_id
                ",
                readRowFunc: reader => new DeletedBoxLink(
                    ExternalId: reader.GetExtId<BoxLinkExtId>(0)),
                transaction: transaction)
            .WithJsonParameter("$boxIds", boxIds)
            .Execute();
    }
    
    private static List<DeletedBoxMember> DeleteBoxMembers(
        List<int> boxIds,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        if (boxIds.Count == 0)
            return [];

        return dbWriteContext
            .Cmd(
                sql: @"
                    DELETE FROM bm_box_membership
                    WHERE
                        bm_box_id IN (
                            SELECT value FROM json_each($boxIds)
                        )
                    RETURNING 
                        bm_member_id,
                        bm_was_invitation_accepted,
                        (SELECT u_email FROM u_users WHERE u_id = bm_member_id) AS bm_member_email,
                        (SELECT bo_external_id FROM bo_boxes WHERE bo_id = bm_box_id) AS bm_box_external_id,                        
                        (SELECT bo_name FROM bo_boxes WHERE bo_id = bm_box_id) AS bm_box_name                        
                ",
                readRowFunc: reader => new DeletedBoxMember(
                    MemberId: reader.GetInt32(0),
                    WasInvitationAccepted: reader.GetBoolean(1),
                    Email: reader.GetEmail(2),
                    BoxExternalId: reader.GetExtId<BoxExtId>(3),
                    BoxName: reader.GetString(4)),
                transaction: transaction)
            .WithJsonParameter("$boxIds", boxIds)
            .Execute();
    }
    
    private static List<DeletedBox> DeleteBoxes(
        int workspaceId,
        List<int> boxIds, 
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        if (boxIds.Count == 0)
            return [];

        return dbWriteContext
            .Cmd(
                sql: @"
                    DELETE FROM bo_boxes
                    WHERE
                        bo_id IN (
                            SELECT value FROM json_each($boxIds)
                        )
                        AND bo_workspace_id = $workspaceId
                    RETURNING 
                        bo_external_id
                ",
                readRowFunc: reader => new DeletedBox(
                    ExternalId: reader.GetExtId<BoxExtId>(0)),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .WithJsonParameter("$boxIds", boxIds)
            .Execute();
    }

    public readonly record struct Result(
        BoxExtId[] DeletedBoxes,
        BoxMember[] DeletedBoxMembers);

    private readonly record struct DeletedBoxMember(
        int MemberId,
        BoxExtId BoxExternalId,
        Email Email,
        string BoxName,
        bool WasInvitationAccepted);

    private readonly record struct DeletedBoxLink(
        BoxLinkExtId ExternalId);

    private readonly record struct DeletedBox(
        BoxExtId ExternalId);
    
    public readonly record struct BoxMember(
        BoxExtId BoxExternalId,
        int MemberId
    );
}