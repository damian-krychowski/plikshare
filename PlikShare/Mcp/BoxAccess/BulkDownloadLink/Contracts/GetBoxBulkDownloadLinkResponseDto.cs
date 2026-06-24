namespace PlikShare.Mcp.BoxAccess.BulkDownloadLink.Contracts;

public class GetBoxBulkDownloadLinkResponseDto
{
    public required string Url { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
}
