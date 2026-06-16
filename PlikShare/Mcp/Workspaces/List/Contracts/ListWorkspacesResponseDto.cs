namespace PlikShare.Mcp.Workspaces.List.Contracts;

public class ListWorkspacesResponseDto
{
    public required List<WorkspaceItemDto> Workspaces { get; init; }
}

public class WorkspaceItemDto
{
    public required string WorkspaceExternalId { get; init; }
    public required string Name { get; init; }
    public required long CurrentSizeInBytes { get; init; }
}
