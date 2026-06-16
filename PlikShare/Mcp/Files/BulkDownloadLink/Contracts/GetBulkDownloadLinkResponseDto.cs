namespace PlikShare.Mcp.Files.BulkDownloadLink.Contracts;

public class GetBulkDownloadLinkResponseDto
{
    public required string Url { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
}
