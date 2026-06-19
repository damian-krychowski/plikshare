namespace PlikShare.Mcp.BoxLinks.Create;

public class CreateBoxLinkParams
{
    public required string WorkspaceExternalId { get; init; }
    public required string BoxExternalId { get; init; }
    public required string Name { get; init; }
}
