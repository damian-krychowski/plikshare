using System.ComponentModel;
using Microsoft.Extensions.Caching.Hybrid;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Users.Cache;
using PlikShare.Workspaces.Id;
using PlikShare.Workspaces.Permissions;

namespace PlikShare.Workspaces.Cache;

public class WorkspaceMembershipCache(
    PlikShareDb plikShareDb,
    HybridCache cache,
    WorkspaceCache workspaceCache,
    UserCache userCache)
{
    private static string WorkspaceMembershipKey(WorkspaceExtId workspaceExternalId, int memberId) 
        => $"workspace-membership:workspace-external-id:{workspaceExternalId}:member-id:{memberId}";
    
    public async ValueTask<WorkspaceMembershipContext?> TryGetWorkspaceMembership(
        WorkspaceExtId workspaceExternalId,
        int memberId,
        CancellationToken cancellationToken)
    {
        var workspace = await workspaceCache.TryGetWorkspace(
            workspaceExternalId,
            cancellationToken);

        var user = await userCache.TryGetUser(
            memberId,
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
                User: workspace.Owner,
                Permissions: new WorkspacePermissions(AllowShare: true),
                Invitation: null);
        }
        
        var cachedMembership = await cache.GetOrCreateAsync(
            key: WorkspaceMembershipKey(workspace.ExternalId, member.Id),
            factory: _ => ValueTask.FromResult(LoadMembership(workspace.Id, member.Id)),
            cancellationToken: cancellationToken);

        if (cachedMembership is null)
            return null;

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

    public async ValueTask InvalidateEntry(
        int workspaceId,
        int memberId,
        CancellationToken cancellationToken)
    {
        var workspace = await workspaceCache.TryGetWorkspace(
            workspaceId,
            cancellationToken);
        
        if(workspace is null)
            return;
        
        var key = WorkspaceMembershipKey(
            workspace.ExternalId,
            memberId);
        
        await cache.RemoveAsync(
            key, 
            cancellationToken);
    }
    
    public ValueTask InvalidateEntry(
        WorkspaceExtId workspaceExternalId,
        int memberId,
        CancellationToken cancellationToken)
    {
        var key = WorkspaceMembershipKey(
            workspaceExternalId,
            memberId);
        
        return cache.RemoveAsync(
            key, 
            cancellationToken);
    }
    
    public ValueTask InvalidateEntry(
        WorkspaceMembershipContext workspaceMembership,
        CancellationToken cancellationToken)
    {
        var key = WorkspaceMembershipKey(
            workspaceMembership.Workspace.ExternalId,
            workspaceMembership.User.Id);
        
        return cache.RemoveAsync(
            key, 
            cancellationToken);
    }
    
    private WorkspaceMembershipCached? LoadMembership(
        int workspaceId,
        int memberId)
    {
        using var connection = plikShareDb.OpenConnection();
        
        var (isEmpty, membership) =  connection
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

        return isEmpty ? null : membership;
    }

    [ImmutableObject(true)]
    public sealed record WorkspaceMembershipCached(
        int InviterId,
        bool WasInvitationAccepted,
        WorkspacePermissions Permissions);
}