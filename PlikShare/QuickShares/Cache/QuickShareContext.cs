using PlikShare.QuickShares.Id;
using PlikShare.Users.Id;
using PlikShare.Workspaces.Cache;

namespace PlikShare.QuickShares.Cache;

public record QuickShareContext(
    int Id,
    QuickShareExtId ExternalId,
    string Name,
    WorkspaceContext Workspace,
    UserExtId CreatorExternalId,
    string Slug,
    byte[]? SecretHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    string? PasswordHash,
    byte[]? PasswordSalt,
    int? MaxDownloads,
    int DownloadsCount,
    QuickShareMode Mode,
    bool AllowIndividualFileDownload,
    DateTimeOffset? LastAccessedAt);
