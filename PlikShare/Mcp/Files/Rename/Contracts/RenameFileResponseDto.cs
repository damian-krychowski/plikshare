namespace PlikShare.Mcp.Files.Rename.Contracts;

public class RenameFileResponseDto
{
    public required string FileExternalId { get; init; }
    public required string Name { get; init; }
}
