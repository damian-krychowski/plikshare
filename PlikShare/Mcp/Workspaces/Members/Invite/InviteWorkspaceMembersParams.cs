namespace PlikShare.Mcp.Workspaces.Members.Invite;

public class InviteWorkspaceMembersParams
{
    public required string WorkspaceExternalId { get; init; }
    public required string[] MemberEmails { get; init; }
    public required bool AllowShare { get; init; }
}
