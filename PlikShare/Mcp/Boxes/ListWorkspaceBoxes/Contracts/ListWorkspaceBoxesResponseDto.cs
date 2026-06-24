namespace PlikShare.Mcp.Boxes.ListWorkspaceBoxes.Contracts;

public class ListWorkspaceBoxesResponseDto
{
    public required List<BoxDto> Boxes { get; init; }

    public class BoxDto
    {
        public required string ExternalId { get; init; }
        public required string Name { get; init; }
        public required bool IsEnabled { get; init; }
        public required List<FolderPathItemDto> FolderPath { get; init; }
    }

    public class FolderPathItemDto
    {
        public required string ExternalId { get; init; }
        public required string Name { get; init; }
    }
}
