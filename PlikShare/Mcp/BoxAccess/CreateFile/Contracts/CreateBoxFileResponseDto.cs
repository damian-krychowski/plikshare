namespace PlikShare.Mcp.BoxAccess.CreateFile.Contracts;

public class CreateBoxFileResponseDto
{
    public required string FileExternalId { get; init; }
    public required string Name { get; init; }
    public required string FolderExternalId { get; init; }
}
