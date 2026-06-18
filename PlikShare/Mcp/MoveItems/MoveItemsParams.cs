namespace PlikShare.Mcp.MoveItems;

public class MoveItemsParams
{
    public required string WorkspaceExternalId { get; init; }
    public required string[] FolderExternalIds { get; init; }
    public required string[] FileExternalIds { get; init; }
    public required string? DestinationFolderExternalId { get; init; }
}
