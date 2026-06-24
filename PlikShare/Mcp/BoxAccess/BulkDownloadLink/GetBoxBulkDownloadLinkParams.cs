namespace PlikShare.Mcp.BoxAccess.BulkDownloadLink;

public class GetBoxBulkDownloadLinkParams
{
    public required string BoxExternalId { get; init; }
    public required string[] FileExternalIds { get; init; }
    public required string[] FolderExternalIds { get; init; }
    public required string[] ExcludedFileExternalIds { get; init; }
    public required string[] ExcludedFolderExternalIds { get; init; }
    public required int? ExpiresInMinutes { get; init; }
}
