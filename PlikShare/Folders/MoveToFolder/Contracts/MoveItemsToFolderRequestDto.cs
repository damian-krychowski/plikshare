using PlikShare.Files.Id;
using PlikShare.Folders.Id;
using PlikShare.Uploads.Id;

namespace PlikShare.Folders.MoveToFolder.Contracts;

public record MoveItemsToFolderRequestDto(
    FileExtId[] FileExternalIds,
    FolderExtId[] FolderExternalIds,
    FileUploadExtId[] FileUploadExternalIds,
    FolderExtId? DestinationFolderExternalId);