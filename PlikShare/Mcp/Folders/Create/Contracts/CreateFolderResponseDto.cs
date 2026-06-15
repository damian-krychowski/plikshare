namespace PlikShare.Mcp.Folders.Create.Contracts;

public class CreateFolderResponseDto
{
    public required string FolderExternalId { get; init; }
    public required string Name { get; init; }
    public required string? ParentFolderExternalId { get; init; }
}
