using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.BoxAccess.GetDetails.Contracts;

/// <summary>
/// get_box_details reads a single box the agent was granted direct access to; its details carry the box
/// id and current name so a human reviewing the approval sees which box would be read.
/// </summary>
public class GetBoxDetailsOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.GetBoxDetails;

    public required string BoxExternalId { get; init; }
    public required string? BoxName { get; init; }
}
