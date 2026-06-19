namespace PlikShare.Mcp.Workspaces.Members.UpdatePermissions;

public class UpdateWorkspaceMemberPermissionsParams
{
    public required string WorkspaceExternalId { get; init; }
    public required string MemberExternalId { get; init; }
    public required bool AllowShare { get; init; }
}
