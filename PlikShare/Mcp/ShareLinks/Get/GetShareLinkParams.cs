namespace PlikShare.Mcp.ShareLinks.Get;

public class GetShareLinkParams
{
    public required string WorkspaceExternalId { get; init; }
    public required string ShareLinkExternalId { get; init; }
}
