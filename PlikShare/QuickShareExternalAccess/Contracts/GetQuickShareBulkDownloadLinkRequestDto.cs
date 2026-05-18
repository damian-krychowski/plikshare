using PlikShare.Files.Id;
using PlikShare.Folders.Id;

namespace PlikShare.QuickShareExternalAccess.Contracts;

public record GetQuickShareBulkDownloadLinkRequestDto(
    FolderExtId[]? SelectedFolderExternalIds,
    FolderExtId[]? ExcludedFolderExternalIds,
    FileExtId[]? SelectedFileExternalIds,
    FileExtId[]? ExcludedFileExternalIds);
