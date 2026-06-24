namespace PlikShare.Mcp.BoxAccess.DownloadLink;

public class GetBoxFileDownloadLinkParams
{
    public required string BoxExternalId { get; init; }
    public required string FileExternalId { get; init; }
    public required int? ExpiresInMinutes { get; init; }
}
