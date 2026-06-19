using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.Boxes.Members.List.Contracts;

/// <summary>
/// list_box_members reads the members of a single box; its details carry the box id so a human reviewing
/// the approval sees which box's members would be read.
/// </summary>
public class ListBoxMembersOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.ListBoxMembers;

    public required string WorkspaceExternalId { get; init; }
    public required string BoxExternalId { get; init; }
}
