namespace PlikShare.Mcp.ShareLinks.Delete;

public class DeleteShareLinkParams
{
    public required string WorkspaceExternalId { get; init; }
    public required string ShareLinkExternalId { get; init; }
}
