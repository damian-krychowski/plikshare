namespace PlikShare.Mcp.BoxAccess.DownloadLink.Contracts;

public class GetBoxFileDownloadLinkResponseDto
{
    public required string Url { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
}
