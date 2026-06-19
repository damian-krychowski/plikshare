namespace PlikShare.Mcp.BoxLinks.Delete;

public class DeleteBoxLinkParams
{
    public required string WorkspaceExternalId { get; init; }
    public required string BoxLinkExternalId { get; init; }
}
