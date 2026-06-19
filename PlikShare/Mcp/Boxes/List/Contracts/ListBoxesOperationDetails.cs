using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.Boxes.List.Contracts;

/// <summary>
/// list_boxes reads the boxes of a single workspace; its details carry the workspace id so a human
/// reviewing the approval sees which workspace's boxes would be read.
/// </summary>
public class ListBoxesOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.ListBoxes;

    public required string WorkspaceExternalId { get; init; }
}
