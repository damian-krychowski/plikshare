using PlikShare.QuickShares.Id;

namespace PlikShare.QuickShares.List.Contracts;

public record GetQuickSharesResponseDto(
    List<GetQuickSharesItemDto> Items);

public record GetQuickSharesItemDto(
    QuickShareExtId ExternalId,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    bool HasPassword,
    int? MaxDownloads,
    int DownloadsCount,
    QuickShareMode Mode,
    bool AllowIndividualFileDownload,
    DateTimeOffset? LastAccessedAt,
    string Slug,
    bool HasSecret,
    string? Url,
    int SelectedFilesCount,
    int SelectedFoldersCount,
    int ExcludedFilesCount,
    int ExcludedFoldersCount);
