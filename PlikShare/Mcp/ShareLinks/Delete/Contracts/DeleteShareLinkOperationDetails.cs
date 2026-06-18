using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.ShareLinks.Delete.Contracts;

public class DeleteShareLinkOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.DeleteShareLink;

    public required string ExternalId { get; init; }
    public required string? Name { get; init; }
}
