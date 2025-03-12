using PlikShare.Files.Id;
using PlikShare.Folders.Id;

namespace PlikShare.Files.BulkDownload.Contracts;

public class GetBulkDownloadLinkRequestDto
{
    public required List<FolderExtId> SelectedFolders { get; init; }
    public required List<FileExtId> SelectedFiles { get; init; }
    public required List<FolderExtId> ExcludedFolders { get; init; }
    public required List<FileExtId> ExcludedFiles { get; init; }
}

public class GetBulkDownloadLinkResponseDto
{
    public required string PreSignedUrl { get; init; }
}