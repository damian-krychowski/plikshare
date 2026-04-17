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
    private static readonly HybridCacheEntryOptions ProbeOptions = new()
    {
        Flags = HybridCacheEntryFlags.DisableLocalCacheWrite
              | HybridCacheEntryFlags.DisableDistributedCacheWrite
    };

    private static string MembershipKey(int boxId, int memberId)
        => $"box-membership:{boxId}:{memberId}";

    private static string BoxMembershipsTag(int boxId)
        => $"box-memberships-{boxId}";

    private static string MemberBoxMembershipsTag(int memberId)
        => $"member-box-memberships-{memberId}";

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
            workspaceId: box.Workspace.Id,
            memberId: member.Id,
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

        var cachedMembership = await ProbeMembershipCache(
            box.Id,
            member.Id,
            cancellationToken);

        if (cachedMembership is null)
        {
            var loaded = LoadMembership(box.Id, member.Id);

            if (loaded is null)
                return null;

            await StoreMembership(
                box.Id,
                member.Id,
                loaded,
                cancellationToken);

            cachedMembership = loaded;
        }

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

    private async ValueTask<BoxMembershipCached?> ProbeMembershipCache(
        int boxId,
        int memberId,
        CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync<BoxMembershipCached?>(
            key: MembershipKey(boxId, memberId),
            factory: _ => ValueTask.FromResult<BoxMembershipCached?>(null),
            options: ProbeOptions,
            cancellationToken: cancellationToken);
    }

    private ValueTask StoreMembership(
        int boxId,
        int memberId,
        BoxMembershipCached membership,
        CancellationToken cancellationToken)
    {
        var tags = new[]
        {
            BoxMembershipsTag(boxId),
            MemberBoxMembershipsTag(memberId)
        };

        return cache.SetAsync(
            MembershipKey(boxId, memberId),
            membership,
            tags: tags,
            cancellationToken: cancellationToken);
    }

    public ValueTask InvalidateEntry(
        int boxId,
        int memberId,
        CancellationToken cancellationToken)
    {
        return cache.RemoveAsync(
            MembershipKey(boxId, memberId),
            cancellationToken);
    }

    public ValueTask InvalidateEntry(
        BoxMembershipContext boxMembership,
        CancellationToken cancellationToken)
    {
        return cache.RemoveAsync(
            MembershipKey(
                boxMembership.Box.Id,
                boxMembership.Member.Id),
            cancellationToken);
    }

    public ValueTask InvalidateAllForBox(
        int boxId,
        CancellationToken cancellationToken)
    {
        return cache.RemoveByTagAsync(
            BoxMembershipsTag(boxId),
            cancellationToken);
    }

    public ValueTask InvalidateAllForMember(
        int memberId,
        CancellationToken cancellationToken)
    {
        return cache.RemoveByTagAsync(
            MemberBoxMembershipsTag(memberId),
            cancellationToken);
    }

    private BoxMembershipCached? LoadMembership(
        int boxId,
        int memberId)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: """
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
                     """,
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

        return result.IsEmpty ? null : result.Value;
    }

    [ImmutableObject(true)]
    public sealed record BoxMembershipCached(
        bool WasInvitationAccepted,
        int InviterId,
        BoxPermissions Permissions);
}