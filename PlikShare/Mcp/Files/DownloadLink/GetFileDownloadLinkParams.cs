namespace PlikShare.Mcp.Files.DownloadLink;

public class GetFileDownloadLinkParams
{
    public required string FileExternalId { get; init; }
    public required int? ExpiresInMinutes { get; init; }
}
