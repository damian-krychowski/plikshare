using PlikShare.Files.Id;
using PlikShare.Folders.Id;

namespace PlikShare.QuickShares.UpdateItems.Contracts;

public record UpdateQuickShareItemsRequestDto(
    List<FileExtId> SelectedFiles,
    List<FolderExtId> SelectedFolders,
    List<FileExtId> ExcludedFiles,
    List<FolderExtId> ExcludedFolders);
