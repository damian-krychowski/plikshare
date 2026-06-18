using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.ShareLinks.List.Contracts;

/// <summary>
/// list_share_links lists every share link in the workspace, which is already shown on the approval —
/// so its details carry only the discriminator.
/// </summary>
public class ListShareLinksOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.ListShareLinks;
}
