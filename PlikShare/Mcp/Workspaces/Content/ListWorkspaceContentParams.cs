namespace PlikShare.Mcp.Workspaces.Content;

public class ListWorkspaceContentParams
{
    public required string WorkspaceExternalId { get; init; }
    public required string? FolderExternalId { get; init; }
    public required string? Type { get; init; }
    public required string? Cursor { get; init; }
    public required int? Limit { get; init; }
}
