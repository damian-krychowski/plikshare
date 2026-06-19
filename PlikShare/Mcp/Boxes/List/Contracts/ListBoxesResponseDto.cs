namespace PlikShare.Mcp.Boxes.List.Contracts;

public class ListBoxesResponseDto
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
