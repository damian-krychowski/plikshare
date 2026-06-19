namespace PlikShare.Mcp.Boxes.Create;

public class CreateBoxParams
{
    public required string WorkspaceExternalId { get; init; }
    public required string Name { get; init; }
    public required string FolderExternalId { get; init; }
}
