using System.ComponentModel;
using Microsoft.Extensions.Caching.Hybrid;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Users.Cache;
using PlikShare.Users.Id;
using PlikShare.Workspaces.Id;
using PlikShare.Workspaces.Permissions;

namespace PlikShare.Workspaces.Cache;

public class WorkspaceMembershipCache(
    PlikShareDb plikShareDb,
    HybridCache cache,
    WorkspaceCache workspaceCache,
    UserCache userCache)
{
    private static readonly HybridCacheEntryOptions ProbeOptions = new()
    {
        Flags = HybridCacheEntryFlags.DisableLocalCacheWrite
              | HybridCacheEntryFlags.DisableDistributedCacheWrite
    };

    private static string MembershipKey(int workspaceId, int memberId)
        => $"workspace-membership:{workspaceId}:{memberId}";

    private static string WorkspaceMembershipsTag(int workspaceId)
        => $"workspace-memberships-{workspaceId}";

    private static string MemberMembershipsTag(int memberId)
        => $"member-memberships-{memberId}";

    public async ValueTask<WorkspaceMembershipContext?> TryGetWorkspaceMembership(
        int workspaceId,
        int memberId,
        CancellationToken cancellationToken)
    {
        var workspace = await workspaceCache.TryGetWorkspace(
            workspaceId,
            cancellationToken);

        var user = await userCache.TryGetUser(
            memberId,
            cancellationToken);

        return await TryGetWorkspaceMembership(
            workspace: workspace,
            member: user,
            cancellationToken: cancellationToken);
    }

    public async ValueTask<WorkspaceMembershipContext?> TryGetWorkspaceMembership(
        WorkspaceExtId workspaceExternalId,
        UserExtId memberExternalId,
        CancellationToken cancellationToken)
    {
        var workspace = await workspaceCache.TryGetWorkspace(
            workspaceExternalId,
            cancellationToken);

        var user = await userCache.TryGetUser(
            memberExternalId,
            cancellationToken);

        return await TryGetWorkspaceMembership(
            workspace: workspace,
            member: user,
            cancellationToken: cancellationToken);
    }

    private async ValueTask<WorkspaceMembershipContext?> TryGetWorkspaceMembership(
        WorkspaceContext? workspace,
        UserContext? member,
        CancellationToken cancellationToken)
    {
        if (workspace is null || member is null)
            return null;

        if (member.HasAdminRole)
        {
            return new WorkspaceMembershipContext(
                Workspace: workspace,
                User: member,
                Permissions: new WorkspacePermissions(AllowShare: true),
                Invitation: null);
        }

        if (workspace.Owner.Id == member.Id)
        {
            return new WorkspaceMembershipContext(
                Workspace: workspace,
                User: member,
                Permissions: new WorkspacePermissions(AllowShare: true),
                Invitation: null);
        }

        var cachedMembership = await ProbeMembershipCache(
            workspace.Id,
            member.Id,
            cancellationToken);

        if (cachedMembership is null)
        {
            var loaded = LoadMembership(
                workspace.Id, 
                member.Id);

            if (loaded is null)
                return null;

            await StoreMembership(
                workspace.Id,
                member.Id,
                loaded,
                cancellationToken);

            cachedMembership = loaded;
        }

        var inviter = await userCache.TryGetUser(
            cachedMembership.InviterId,
            cancellationToken: cancellationToken);

        return new WorkspaceMembershipContext(
            User: member,
            Workspace: workspace,
            Permissions: cachedMembership.Permissions,
            Invitation: new WorkspaceInvitation(
                WasInvitationAccepted: cachedMembership.WasInvitationAccepted,
                Inviter: inviter));
    }

    private async ValueTask<WorkspaceMembershipCached?> ProbeMembershipCache(
        int workspaceId,
        int memberId,
        CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync<WorkspaceMembershipCached?>(
            key: MembershipKey(workspaceId, memberId),
            factory: _ => ValueTask.FromResult<WorkspaceMembershipCached?>(null),
            options: ProbeOptions,
            cancellationToken: cancellationToken);
    }

    private ValueTask StoreMembership(
        int workspaceId,
        int memberId,
        WorkspaceMembershipCached membership,
        CancellationToken cancellationToken)
    {
        var tags = new[]
        {
            WorkspaceMembershipsTag(workspaceId),
            MemberMembershipsTag(memberId)
        };

        return cache.SetAsync(
            MembershipKey(workspaceId, memberId),
            membership,
            tags: tags,
            cancellationToken: cancellationToken);
    }

    public ValueTask InvalidateEntry(
        int workspaceId,
        int memberId,
        CancellationToken cancellationToken)
    {
        return cache.RemoveAsync(
            MembershipKey(workspaceId, memberId),
            cancellationToken);
    }

    public ValueTask InvalidateEntry(
        WorkspaceMembershipContext workspaceMembership,
        CancellationToken cancellationToken)
    {
        return cache.RemoveAsync(
            MembershipKey(
                workspaceMembership.Workspace.Id,
                workspaceMembership.User.Id),
            cancellationToken);
    }

    public ValueTask InvalidateAllForWorkspace(
        int workspaceId,
        CancellationToken cancellationToken)
    {
        return cache.RemoveByTagAsync(
            WorkspaceMembershipsTag(workspaceId),
            cancellationToken);
    }

    public ValueTask InvalidateAllForMember(
        int memberId,
        CancellationToken cancellationToken)
    {
        return cache.RemoveByTagAsync(
            MemberMembershipsTag(memberId),
            cancellationToken);
    }

    private WorkspaceMembershipCached? LoadMembership(
        int workspaceId,
        int memberId)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: """
                     SELECT
                         wm_inviter_id,
                         wm_was_invitation_accepted,
                         wm_allow_share
                     FROM wm_workspace_membership
                     WHERE
                         wm_workspace_id = $workspaceId
                         AND wm_member_id = $memberId
                     LIMIT 1
                     """,
                readRowFunc: reader => new WorkspaceMembershipCached(
                    InviterId: reader.GetInt32(0),
                    WasInvitationAccepted: reader.GetBoolean(1),
                    Permissions: new WorkspacePermissions(
                        AllowShare: reader.GetBoolean(2))))
            .WithParameter("$workspaceId", workspaceId)
            .WithParameter("$memberId", memberId)
            .Execute();

        return result.IsEmpty ? null : result.Value;
    }

    [ImmutableObject(true)]
    public sealed record WorkspaceMembershipCached(
        int InviterId,
        bool WasInvitationAccepted,
        WorkspacePermissions Permissions);
}