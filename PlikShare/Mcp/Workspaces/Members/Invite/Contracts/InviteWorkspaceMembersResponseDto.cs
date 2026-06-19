namespace PlikShare.Mcp.Workspaces.Members.Invite.Contracts;

public class InviteWorkspaceMembersResponseDto
{
    public required List<InvitedMember> Members { get; init; }

    public class InvitedMember
    {
        public required string ExternalId { get; init; }
        public required string Email { get; init; }
    }
}
