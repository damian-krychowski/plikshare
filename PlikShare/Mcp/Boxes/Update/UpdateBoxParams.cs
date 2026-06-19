namespace PlikShare.Mcp.Boxes.Update;

public class UpdateBoxParams
{
    public required string WorkspaceExternalId { get; init; }
    public required string BoxExternalId { get; init; }
    public required string? Name { get; init; }
    public required bool? IsEnabled { get; init; }
    public required string? FolderExternalId { get; init; }
}
