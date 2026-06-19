namespace PlikShare.Mcp.Boxes.Members.Invite;

public class InviteBoxMembersParams
{
    public required string WorkspaceExternalId { get; init; }
    public required string BoxExternalId { get; init; }
    public required string[] MemberEmails { get; init; }
}
