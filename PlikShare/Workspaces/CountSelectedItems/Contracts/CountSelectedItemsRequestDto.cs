using PlikShare.Files.Id;
using PlikShare.Folders.Id;

namespace PlikShare.Workspaces.CountSelectedItems.Contracts;

public class CountSelectedItemsRequestDto
{
    public required List<FolderExtId> SelectedFolders { get; init; }
    public required List<FileExtId> SelectedFiles { get; init; }
    public required List<FolderExtId> ExcludedFolders { get; init; }
    public required List<FileExtId> ExcludedFiles { get; init; }
}

public class CountSelectedItemsResponseDto
{
    public int SelectedFoldersCount { get; init; }
    public int SelectedFilesCount { get; init; }
    public long TotalSizeInBytes { get; init; }
}