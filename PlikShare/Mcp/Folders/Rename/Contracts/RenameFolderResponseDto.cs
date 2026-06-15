namespace PlikShare.Mcp.Folders.Rename.Contracts;

public class RenameFolderResponseDto
{
    public required string FolderExternalId { get; init; }
    public required string Name { get; init; }
}
