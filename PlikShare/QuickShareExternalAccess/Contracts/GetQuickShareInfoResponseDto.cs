namespace PlikShare.QuickShareExternalAccess.Contracts;

public record GetQuickShareInfoResponseDto(
    string Name,
    QuickShares.QuickShareMode Mode,
    bool AllowIndividualFileDownload,
    bool RequiresPassword,
    bool IsUnlocked,
    bool IsExpired,
    bool IsExhausted,
    bool IsOwnerPreview,
    DateTimeOffset? ExpiresAt,
    int? MaxDownloads,
    int DownloadsCount);
