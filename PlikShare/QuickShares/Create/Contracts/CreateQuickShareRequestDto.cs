using PlikShare.Files.Id;
using PlikShare.Folders.Id;

namespace PlikShare.QuickShares.Create.Contracts;

public record CreateQuickShareRequestDto(
    string Name,
    string? CustomSlug,
    List<FileExtId> SelectedFiles,
    List<FolderExtId> SelectedFolders,
    List<FileExtId> ExcludedFiles,
    List<FolderExtId> ExcludedFolders,
    QuickShareMode Mode,
    bool AllowIndividualFileDownload,
    DateTimeOffset? ExpiresAt,
    string? Password,
    int? MaxDownloads);
