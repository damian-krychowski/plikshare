using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.BoxAccess.BulkDownloadLink.Contracts;

/// <summary>
/// get_box_bulk_download_link mints a short-lived ZIP download link for several items inside a box; its
/// details carry the box (id and name), the names and paths of the folders and files that would be
/// bundled (and any carved out), plus the link's lifetime, so a human reviewing the approval sees
/// exactly what would be downloadable and for how long.
/// </summary>
public class GetBoxBulkDownloadLinkOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.GetBoxBulkDownloadLink;

    public required string BoxExternalId { get; init; }
    public required string? BoxName { get; init; }
    public required List<DownloadItem> Folders { get; init; }
    public required List<DownloadItem> Files { get; init; }
    public required List<DownloadItem> ExcludedFolders { get; init; }
    public required List<DownloadItem> ExcludedFiles { get; init; }
    public required int? ExpiresInMinutes { get; init; }

    public class DownloadItem
    {
        public required string ExternalId { get; init; }
        public required string Name { get; init; }
        public string? Path { get; init; }
        public string? FolderExternalId { get; init; }
    }
}
