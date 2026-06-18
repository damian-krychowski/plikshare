using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.Storages.List.Contracts;

/// <summary>
/// list_storages takes no target — it lists everything the agent can use — so its approval details
/// carry only the discriminator.
/// </summary>
public class ListStoragesOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.ListStorages;
}
