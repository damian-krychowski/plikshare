using PlikShare.QuickShares.Id;
using PlikShare.Users.Id;

namespace PlikShare.QuickShares.Get.Contracts;

public record GetQuickShareResponseDto(
    QuickShareExtId ExternalId,
    string Name,
    UserExtId CreatorExternalId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    bool HasPassword,
    int? MaxDownloads,
    int DownloadsCount,
    QuickShareMode Mode,
    bool AllowIndividualFileDownload,
    DateTimeOffset? LastAccessedAt,
    string AccessCodeStatus,
    string? Url,
    GetQuickShareItemsDto Items);
