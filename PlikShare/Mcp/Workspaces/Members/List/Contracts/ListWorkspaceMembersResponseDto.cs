namespace PlikShare.Mcp.Workspaces.Members.List.Contracts;

public class ListWorkspaceMembersResponseDto
{
    public required List<WorkspaceMemberDto> Members { get; init; }

    public class WorkspaceMemberDto
    {
        public required string MemberExternalId { get; init; }
        public required string Email { get; init; }
        public required string? InviterEmail { get; init; }
        public required bool InvitationAccepted { get; init; }
        public required bool AllowShare { get; init; }
    }
}
