using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.ShareLinks.Get.Contracts;

public class GetShareLinkOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.GetShareLink;

    public required string ShareLinkExternalId { get; init; }
    public required string? Name { get; init; }
}
