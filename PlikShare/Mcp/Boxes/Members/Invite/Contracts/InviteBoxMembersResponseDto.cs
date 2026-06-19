namespace PlikShare.Mcp.Boxes.Members.Invite.Contracts;

public class InviteBoxMembersResponseDto
{
    public required List<InvitedMember> Members { get; init; }

    public class InvitedMember
    {
        public required string ExternalId { get; init; }
        public required string Email { get; init; }
    }
}
