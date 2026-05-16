using PlikShare.Files.Id;
using PlikShare.Folders.Id;

namespace PlikShare.QuickShares.Get.Contracts;

public record GetQuickShareItemsDto(
    List<FileExtId> SelectedFiles,
    List<FolderExtId> SelectedFolders,
    List<FileExtId> ExcludedFiles,
    List<FolderExtId> ExcludedFolders);
