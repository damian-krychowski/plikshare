namespace PlikShare.Mcp.MoveItems.Contracts;

public class MoveItemsResponseDto
{
    public required int MovedFolderCount { get; init; }
    public required int MovedFileCount { get; init; }
    public required string? DestinationFolderExternalId { get; init; }
}
