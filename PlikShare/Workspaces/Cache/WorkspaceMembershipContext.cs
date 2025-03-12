using PlikShare.Users.Cache;
using PlikShare.Workspaces.Permissions;

namespace PlikShare.Workspaces.Cache;

public record WorkspaceMembershipContext(
    UserContext User,
    WorkspaceContext Workspace,
    WorkspacePermissions Permissions,
    WorkspaceInvitation? Invitation)
{
    public bool IsAvailableForUser =>
        !Workspace.IsBeingDeleted && (User.HasAdminRole || IsOwnedByUser || Invitation is { WasInvitationAccepted: true });
    
    public bool IsOwnedByUser => User.Id == Workspace.Owner.Id;
    public bool WasUserInvited => Invitation is not null;
}

public record WorkspaceInvitation(
    bool WasInvitationAccepted,
    UserContext? Inviter);