using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.BoxAccess.Content.Contracts;

/// <summary>
/// list_box_content lists the folders and files inside a box; its details carry the box (id and name)
/// and the folder being listed (its id and name, or the box root) so a human reviewing the approval
/// sees which part of the box would be read.
/// </summary>
public class ListBoxContentOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.ListBoxContent;

    public required string BoxExternalId { get; init; }
    public required string? BoxName { get; init; }
    public required string? FolderExternalId { get; init; }
    public required string? FolderName { get; init; }
}
