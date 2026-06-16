namespace PlikShare.Mcp.BulkDelete.Contracts;

public class BulkDeleteResponseDto
{
    public required int DeletedFileCount { get; init; }
    public required long DeletedSizeInBytes { get; init; }
}
