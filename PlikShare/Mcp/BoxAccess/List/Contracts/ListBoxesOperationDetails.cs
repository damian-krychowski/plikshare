using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.BoxAccess.List.Contracts;

/// <summary>
/// list_boxes takes no target — it lists every box shared directly with the agent — so its approval
/// details carry only the discriminator.
/// </summary>
public class ListBoxesOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.ListBoxes;
}
