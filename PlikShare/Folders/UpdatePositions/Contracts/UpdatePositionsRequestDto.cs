using PlikShare.Folders.Id;

namespace PlikShare.Folders.UpdatePositions.Contracts;

public class UpdatePositionsRequestDto
{
    public required FolderExtId? ParentFolderExternalId { get; init; }
    public required List<UpdatePositionItemDto> Folders { get; init; }
    public required List<UpdatePositionItemDto> Files { get; init; }
}

public class UpdatePositionItemDto
{
    public required string ExternalId { get; init; }
    public required long Position { get; init; }
}
