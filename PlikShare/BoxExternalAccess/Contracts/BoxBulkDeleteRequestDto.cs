using PlikShare.Files.Id;
using PlikShare.Folders.Id;
using PlikShare.Uploads.Id;

namespace PlikShare.BoxExternalAccess.Contracts;

public record BoxBulkDeleteRequestDto(
    List<FileExtId> FileExternalIds, 
    List<FolderExtId> FolderExternalIds,
    List<FileUploadExtId> FileUploadExternalIds);