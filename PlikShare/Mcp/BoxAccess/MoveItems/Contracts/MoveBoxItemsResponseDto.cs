namespace PlikShare.Mcp.BoxAccess.MoveItems.Contracts;

public class MoveBoxItemsResponseDto
{
    public required int MovedFolderCount { get; init; }
    public required int MovedFileCount { get; init; }
    public required string DestinationFolderExternalId { get; init; }
}
