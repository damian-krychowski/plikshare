namespace PlikShare.Mcp.BoxAccess.Delete.Contracts;

public class DeleteBoxItemsResponseDto
{
    public required int DeletedFileCount { get; init; }
    public required long DeletedSizeInBytes { get; init; }
}
