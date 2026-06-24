namespace PlikShare.Mcp.BoxAccess.RenameFile.Contracts;

public class RenameBoxFileResponseDto
{
    public required string FileExternalId { get; init; }
    public required string Name { get; init; }
}
