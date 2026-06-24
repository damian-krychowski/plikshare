namespace PlikShare.Mcp.BoxAccess.RenameFolder;

public class RenameBoxFolderParams
{
    public required string BoxExternalId { get; init; }
    public required string FolderExternalId { get; init; }
    public required string Name { get; init; }
}
