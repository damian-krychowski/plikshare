using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.Files.BulkDownloadLink.Contracts;

public class GetBulkDownloadLinkOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.GetBulkDownloadLink;

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
    }
}
