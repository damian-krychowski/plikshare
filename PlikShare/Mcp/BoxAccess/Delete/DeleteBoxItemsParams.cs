namespace PlikShare.Mcp.BoxAccess.Delete;

public class DeleteBoxItemsParams
{
    public required string BoxExternalId { get; init; }
    public required string[] FileExternalIds { get; init; }
    public required string[] FolderExternalIds { get; init; }
}
