using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.BoxAccess.MoveItems.Contracts;

/// <summary>
/// move_box_items moves files and folders inside a box; its details carry the box (id and name), the
/// names and paths of the items being moved and the destination folder, so a human reviewing the
/// approval sees exactly what gets moved and where.
/// </summary>
public class MoveBoxItemsOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.MoveBoxItems;

    public required string BoxExternalId { get; init; }
    public required string? BoxName { get; init; }
    public required string? DestinationFolderExternalId { get; init; }
    public required string? DestinationName { get; init; }
    public required string? DestinationPath { get; init; }
    public required List<ItemToMove> Folders { get; init; }
    public required List<ItemToMove> Files { get; init; }

    public class ItemToMove
    {
        public required string ExternalId { get; init; }
        public required string Name { get; init; }
        public string? Path { get; init; }
    }
}
