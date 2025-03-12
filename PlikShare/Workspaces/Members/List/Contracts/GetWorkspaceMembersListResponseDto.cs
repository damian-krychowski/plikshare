using PlikShare.Users.Id;

namespace PlikShare.Workspaces.Members.List.Contracts;

public class GetWorkspaceMembersListResponseDto
{
    public required List<Membership> Items { get; init; }
    
    public class Membership
    {
        public required UserExtId MemberExternalId { get; init; }
        public required string? InviterEmail { get; init; }
        public required string MemberEmail { get; init; }
        public required bool WasInvitationAccepted { get; init; }
        public required WorkspacePermissions Permissions { get; init; }
    }

    public class WorkspacePermissions
    {
        public required bool AllowShare { get; init; }
    }
}