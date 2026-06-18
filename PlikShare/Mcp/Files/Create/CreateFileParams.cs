namespace PlikShare.Mcp.Files.Create;

public class CreateFileParams
{
    public required string WorkspaceExternalId { get; init; }
    public required string Name { get; init; }
    public required string Content { get; init; }
    public required string? FolderExternalId { get; init; }
    public required string? ContentType { get; init; }
}
