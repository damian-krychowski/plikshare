namespace PlikShare.Mcp.BoxAccess.CreateFolder.Contracts;

public class CreateBoxFolderResponseDto
{
    public required string FolderExternalId { get; init; }
    public required string Name { get; init; }
    public required string? ParentFolderExternalId { get; init; }
}
