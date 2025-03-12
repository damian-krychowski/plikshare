using PlikShare.Boxes.Id;

namespace PlikShare.Boxes.List.Contracts;

public class GetBoxesResponseDto
{
    public required List<Box> Items { get; init; }
    
    public class Box
    {
        public required BoxExtId ExternalId { get; init; }
        public required string Name { get; init; }
        public required bool IsEnabled { get; init; }
        public required List<FolderItem> FolderPath { get; init; }
    }

    public class FolderItem
    {
        public required string ExternalId { get; init; }
        public required string Name { get; init; }
    }
}