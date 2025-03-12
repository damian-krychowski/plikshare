using PlikShare.Folders.Id;

namespace PlikShare.Boxes.Create.Contracts;

public record CreateBoxRequestDto(
    string Name,
    FolderExtId FolderExternalId);