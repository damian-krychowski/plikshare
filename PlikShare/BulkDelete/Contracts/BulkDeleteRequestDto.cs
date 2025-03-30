using PlikShare.Files.Id;
using PlikShare.Folders.Id;
using PlikShare.Uploads.Id;

namespace PlikShare.BulkDelete.Contracts;

public class BulkDeleteRequestDto
{
    public required List<FileExtId> FileExternalIds { get; init; }
    public required List<FolderExtId> FolderExternalIds { get; init; }
    public required List<FileUploadExtId> FileUploadExternalIds { get; init; }
}

public class BulkDeleteResponseDto
{
    public required long? NewWorkspaceSizeInBytes { get; init; }
}