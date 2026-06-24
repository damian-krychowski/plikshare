namespace PlikShare.Mcp.BoxAccess.RenameFile;

public class RenameBoxFileParams
{
    public required string BoxExternalId { get; init; }
    public required string FileExternalId { get; init; }
    public required string Name { get; init; }
}
