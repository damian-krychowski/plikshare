using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.MoveItems.Contracts;

public class MoveItemsOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.MoveItems;

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
