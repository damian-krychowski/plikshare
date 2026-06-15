using PlikShare.Boxes.Id;

namespace PlikShare.Agents.ListWorkspaceBoxes.Contracts;

public class ListWorkspaceBoxesResponseDto
{
    public required List<Box> Items { get; init; }

    public class Box
    {
        public required BoxExtId ExternalId { get; init; }
        public required string Name { get; init; }
    }
}
