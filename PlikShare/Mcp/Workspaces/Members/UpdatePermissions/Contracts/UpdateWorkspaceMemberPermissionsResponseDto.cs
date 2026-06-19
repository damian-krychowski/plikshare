namespace PlikShare.Mcp.Workspaces.Members.UpdatePermissions.Contracts;

public class UpdateWorkspaceMemberPermissionsResponseDto
{
    public required string MemberExternalId { get; init; }
    public required bool AllowShare { get; init; }
}
