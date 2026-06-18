namespace PlikShare.Mcp.Folders.Create;

public class CreateFolderParams
{
    public required string WorkspaceExternalId { get; init; }
    public required string FolderExternalId { get; init; }
    public required string Name { get; init; }
    public required string? ParentFolderExternalId { get; init; }
}
