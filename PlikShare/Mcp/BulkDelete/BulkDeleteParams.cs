namespace PlikShare.Mcp.BulkDelete;

public class BulkDeleteParams
{
    public required string WorkspaceExternalId { get; init; }
    public required string[] FileExternalIds { get; init; }
    public required string[] FolderExternalIds { get; init; }
}
