using PlikShare.Files.Id;
using PlikShare.Folders.Id;
using PlikShare.Uploads.Id;

namespace PlikShare.BoxExternalAccess.Contracts;

public record MoveBoxItemsToFolderRequestDto(
    FileExtId[] FileExternalIds,
    FolderExtId[] FolderExternalIds,
    FileUploadExtId[] FileUploadExternalIds,
    FolderExtId? DestinationFolderExternalId);