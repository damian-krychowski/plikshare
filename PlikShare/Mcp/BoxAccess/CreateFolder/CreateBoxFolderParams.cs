namespace PlikShare.Mcp.BoxAccess.CreateFolder;

public class CreateBoxFolderParams
{
    public required string BoxExternalId { get; init; }
    public required string FolderExternalId { get; init; }
    public required string Name { get; init; }
    public required string? ParentFolderExternalId { get; init; }
}
