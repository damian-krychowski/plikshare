using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.ShareLinks.Create.Contracts;

public class CreateShareLinkOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.CreateShareLink;

    public required string Name { get; init; }
    public required List<ShareItem> SharedFolders { get; init; }
    public required List<ShareItem> SharedFiles { get; init; }
    public required List<ShareItem> ExcludedFolders { get; init; }
    public required List<ShareItem> ExcludedFiles { get; init; }
    public required string? ExpiresAt { get; init; }
    public required int? MaxDownloads { get; init; }
    public required bool HasPassword { get; init; }

    public class ShareItem
    {
        public required string ExternalId { get; init; }
        public required string Name { get; init; }
        public string? Path { get; init; }
    }
}
