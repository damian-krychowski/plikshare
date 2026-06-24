namespace PlikShare.Mcp.BoxAccess.MoveItems;

public class MoveBoxItemsParams
{
    public required string BoxExternalId { get; init; }
    public required string[] FolderExternalIds { get; init; }
    public required string[] FileExternalIds { get; init; }
    public required string? DestinationFolderExternalId { get; init; }
}
