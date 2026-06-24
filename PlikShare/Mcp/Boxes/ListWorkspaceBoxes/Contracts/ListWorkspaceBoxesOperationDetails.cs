using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.Boxes.ListWorkspaceBoxes.Contracts;

/// <summary>
/// list_workspace_boxes reads the boxes of a single workspace; its details carry the workspace id so a
/// human reviewing the approval sees which workspace's boxes would be read.
/// </summary>
public class ListWorkspaceBoxesOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.ListWorkspaceBoxes;

    public required string WorkspaceExternalId { get; init; }
}
