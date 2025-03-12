using PlikShare.Folders.Id;

namespace PlikShare.Folders.Create.Contracts;

public class CreateFolderRequestDto
{
    public required FolderExtId ExternalId { get; init; }
    public required FolderExtId? ParentExternalId { get; init; }
    public required string Name { get; init; }
}