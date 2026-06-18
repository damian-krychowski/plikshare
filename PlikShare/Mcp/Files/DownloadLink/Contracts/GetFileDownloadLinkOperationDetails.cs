using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.Files.DownloadLink.Contracts;

public class GetFileDownloadLinkOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.GetFileDownloadLink;

    public required string FileExternalId { get; init; }
    public required string? Name { get; init; }
    public required string? Path { get; init; }
    public required int? ExpiresInMinutes { get; init; }
}
