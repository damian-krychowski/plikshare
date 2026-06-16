namespace PlikShare.Mcp.Files.DownloadLink.Contracts;

public class GetFileDownloadLinkResponseDto
{
    public required string Url { get; init; }
    public required string FileName { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
}
