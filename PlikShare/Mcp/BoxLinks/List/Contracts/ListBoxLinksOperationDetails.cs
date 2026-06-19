using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.BoxLinks.List.Contracts;

/// <summary>
/// list_box_links reads the links of a single box; its details carry the box id so a human reviewing the
/// approval sees which box's links would be read.
/// </summary>
public class ListBoxLinksOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.ListBoxLinks;

    public required string WorkspaceExternalId { get; init; }
    public required string BoxExternalId { get; init; }
}
