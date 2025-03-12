using PlikShare.Folders.Id;

namespace PlikShare.Boxes.UpdateFolder.Contracts;

public record UpdateBoxFolderRequestDto(
    FolderExtId FolderExternalId);