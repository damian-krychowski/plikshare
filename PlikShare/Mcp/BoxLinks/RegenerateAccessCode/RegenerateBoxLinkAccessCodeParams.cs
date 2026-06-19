namespace PlikShare.Mcp.BoxLinks.RegenerateAccessCode;

public class RegenerateBoxLinkAccessCodeParams
{
    public required string WorkspaceExternalId { get; init; }
    public required string BoxLinkExternalId { get; init; }
}
