namespace PlikShare.Mcp.BoxAccess.RenameFolder.Contracts;

public class RenameBoxFolderResponseDto
{
    public required string FolderExternalId { get; init; }
    public required string Name { get; init; }
}
