namespace PlikShare.Mcp.Workspaces.Rename.Contracts;

public class RenameWorkspaceResponseDto
{
    public required string WorkspaceExternalId { get; init; }
    public required string Name { get; init; }
}
