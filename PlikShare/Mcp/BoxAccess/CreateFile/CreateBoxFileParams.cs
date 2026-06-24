namespace PlikShare.Mcp.BoxAccess.CreateFile;

public class CreateBoxFileParams
{
    public required string BoxExternalId { get; init; }
    public required string Name { get; init; }
    public required string? Content { get; init; }
    public required string? FolderExternalId { get; init; }
    public required string? ContentType { get; init; }
}
