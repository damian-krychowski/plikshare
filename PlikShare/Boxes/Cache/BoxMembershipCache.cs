using System.ComponentModel;
using Microsoft.Extensions.Caching.Hybrid;
using PlikShare.Boxes.Id;
using PlikShare.Boxes.Permissions;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Users.Cache;
using PlikShare.Users.Id;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Boxes.Cache;

public class BoxMembershipCache(
    PlikShareDb plikShareDb,
    HybridCache cache,
    BoxCache boxCache,
    WorkspaceMembershipCache workspaceMembershipCache,
    UserCache userCache)
{
    private static string BoxMembershipKey(BoxExtId externalId, int memberId) 
        => $"box-membership:box-external-id:{externalId}:member-id:{memberId}";
    
    public async ValueTask<BoxMembershipContext?> TryGetBoxMembership(
        BoxExtId boxExternalId,
        UserExtId memberExternalId,
        CancellationToken cancellationToken)
    {
        var box = await boxCache.TryGetBox(
            boxExternalId,
            cancellationToken);

        var user = await userCache.TryGetUser(
            memberExternalId,
            cancellationToken);

        return await TryGetBoxMembership(
            box: box,
            member: user,
            cancellationToken: cancellationToken);
    }
    
    private async ValueTask<BoxMembershipContext?> TryGetBoxMembership(
        BoxContext? box,
        UserContext? member,
        CancellationToken cancellationToken)
    {
        if (box is null || member is null)
            return null;
            
        var workspaceMembershipContext = await workspaceMembershipCache.TryGetWorkspaceMembership(
            workspaceExternalId: box.Workspace.ExternalId,
            memberExternalId: member.ExternalId,
            cancellationToken: cancellationToken);

        if (workspaceMembershipContext is { IsAvailableForUser: true })
        {
            return new BoxMembershipContext(
                WasInvitationAccepted: null,
                Inviter: null,
                Member: workspaceMembershipContext.User,
                Box: box,
                Permissions: BoxPermissions.Full());
        }

        var cachedMembership = await cache.GetOrCreateAsync(
            key: BoxMembershipKey(box.ExternalId, member.Id),
            factory: _ => ValueTask.FromResult(LoadMembership(box.Id, member.Id)),
            cancellationToken: cancellationToken);

        if (cachedMembership is null)
            return null;

        var inviter = await userCache.TryGetUser(
            userId: cachedMembership.InviterId,
            cancellationToken: cancellationToken);

        return new BoxMembershipContext(
            WasInvitationAccepted: cachedMembership.WasInvitationAccepted,
            Inviter: inviter,
            Member: member,
            Box: box,
            Permissions: cachedMembership.Permissions);
    }

    public async ValueTask InvalidateEntry(
        int boxId, 
        int memberId,
        CancellationToken cancellationToken)
    {
        var box = await boxCache.TryGetBox(
            boxId,
            cancellationToken);
        
        if(box is null)
            return;
        
        var key = BoxMembershipKey(
            box.ExternalId,
            memberId);
        
        await cache.RemoveAsync(
            key, 
            cancellationToken);
    }
    
    public ValueTask InvalidateEntry(
        BoxExtId boxExternalId, 
        int memberId,
        CancellationToken cancellationToken)
    {
        var key = BoxMembershipKey(
            boxExternalId,
            memberId);
        
        return cache.RemoveAsync(
            key, 
            cancellationToken);
    }
    
    public ValueTask InvalidateEntry(
        BoxMembershipContext boxMembership,
        CancellationToken cancellationToken)
    {
        var key = BoxMembershipKey(
            boxMembership.Box.ExternalId,
            boxMembership.Member.Id);
        
        return cache.RemoveAsync(
            key, 
            cancellationToken);
    }
    
    private BoxMembershipCached? LoadMembership(
        int boxId,
        int memberId)
    {
        using var connection = plikShareDb.OpenConnection();

        var (isEmpty, membership) = connection
            .OneRowCmd(
                sql: @"
                      SELECT
                        bm_was_invitation_accepted,
                        bm_inviter_id,
                        bm_allow_download,
                        bm_allow_upload,
                        bm_allow_list,
                        bm_allow_delete_file,
                        bm_allow_rename_file,
                        bm_allow_move_items,
                        bm_allow_create_folder,
                        bm_allow_rename_folder,
                        bm_allow_delete_folder
	                FROM bm_box_membership
                    WHERE
                        bm_box_id = $boxId
                        AND bm_member_id = $memberId
                    LIMIT 1
                ",
                readRowFunc: reader => new BoxMembershipCached(
                    WasInvitationAccepted: reader.GetBoolean(0),
                    InviterId: reader.GetInt32(1),
                    Permissions: new BoxPermissions(
                        AllowDownload: reader.GetBoolean(2),
                        AllowUpload: reader.GetBoolean(3),
                        AllowList: reader.GetBoolean(4),
                        AllowDeleteFile: reader.GetBoolean(5),
                        AllowRenameFile: reader.GetBoolean(6),
                        AllowMoveItems: reader.GetBoolean(7),
                        AllowCreateFolder: reader.GetBoolean(8),
                        AllowRenameFolder: reader.GetBoolean(9),
                        AllowDeleteFolder: reader.GetBoolean(10))))
            .WithParameter("$boxId", boxId)
            .WithParameter("$memberId", memberId)
            .Execute();

        return isEmpty ? null : membership;
    }

    [ImmutableObject(true)]
    public sealed record BoxMembershipCached(
        bool WasInvitationAccepted,
        int InviterId,
        BoxPermissions Permissions);
}