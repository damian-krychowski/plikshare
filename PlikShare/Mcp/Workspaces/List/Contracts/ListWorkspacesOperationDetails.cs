using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.Workspaces.List.Contracts;

/// <summary>
/// list_workspaces takes no target — it lists everything the agent can access — so its approval
/// details carry only the discriminator.
/// </summary>
public class ListWorkspacesOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.ListWorkspaces;
}
