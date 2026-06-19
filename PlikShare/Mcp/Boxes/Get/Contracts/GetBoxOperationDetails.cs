using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.Boxes.Get.Contracts;

/// <summary>
/// get_box reads the details of a single box; its details carry the box id so a human reviewing the
/// approval sees which box would be read.
/// </summary>
public class GetBoxOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.GetBox;

    public required string WorkspaceExternalId { get; init; }
    public required string BoxExternalId { get; init; }
}
