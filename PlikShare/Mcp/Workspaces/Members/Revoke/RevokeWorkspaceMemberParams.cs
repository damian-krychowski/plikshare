namespace PlikShare.Mcp.Workspaces.Members.Revoke;

public class RevokeWorkspaceMemberParams
{
    public required string WorkspaceExternalId { get; init; }
    public required string MemberExternalId { get; init; }
}
