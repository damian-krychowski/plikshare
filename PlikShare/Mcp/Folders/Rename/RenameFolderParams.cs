namespace PlikShare.Mcp.Folders.Rename;

public class RenameFolderParams
{
    public required string WorkspaceExternalId { get; init; }
    public required string FolderExternalId { get; init; }
    public required string Name { get; init; }
}
