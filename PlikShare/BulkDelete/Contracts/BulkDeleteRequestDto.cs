using PlikShare.Files.Id;
using PlikShare.Folders.Id;
using PlikShare.Uploads.Id;

namespace PlikShare.BulkDelete.Contracts;

public record BulkDeleteRequestDto(
    List<FileExtId> FileExternalIds,
    List<FolderExtId> FolderExternalIds,
    List<FileUploadExtId> FileUploadExternalIds);