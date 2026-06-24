namespace PlikShare.Mcp.Workspaces.List.Contracts;

public class ListWorkspacesResponseDto
{
    public required List<WorkspaceItemDto> Workspaces { get; init; }

    /// <summary>
    /// Set only when the agent is a member of no workspace, to point it at the separate box-access
    /// surface - so an empty result does not read as "no access at all". Omitted (null) otherwise.
    /// </summary>
    public string? Hint { get; init; }
}

public class WorkspaceItemDto
{
    public required string WorkspaceExternalId { get; init; }
    public required string Name { get; init; }
    public required long CurrentSizeInBytes { get; init; }
}
