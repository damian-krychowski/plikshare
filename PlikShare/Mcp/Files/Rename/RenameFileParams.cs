namespace PlikShare.Mcp.Files.Rename;

public class RenameFileParams
{
    public required string WorkspaceExternalId { get; init; }
    public required string FileExternalId { get; init; }
    public required string Name { get; init; }
}
