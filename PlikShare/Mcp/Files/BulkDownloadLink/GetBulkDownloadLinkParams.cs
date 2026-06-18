namespace PlikShare.Mcp.Files.BulkDownloadLink;

public class GetBulkDownloadLinkParams
{
    public required string WorkspaceExternalId { get; init; }
    public required string[] FileExternalIds { get; init; }
    public required string[] FolderExternalIds { get; init; }
    public required string[] ExcludedFileExternalIds { get; init; }
    public required string[] ExcludedFolderExternalIds { get; init; }
    public required int? ExpiresInMinutes { get; init; }
}
