namespace PlikShare.Mcp.ShareLinks.List.Contracts;

public class ListShareLinksResponseDto
{
    public required List<ShareLinkListItemDto> ShareLinks { get; init; }
}

public class ShareLinkListItemDto
{
    public required string ExternalId { get; init; }
    public required string Name { get; init; }
    public required string? Url { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset? ExpiresAt { get; init; }
    public required int DownloadsCount { get; init; }
    public required int? MaxDownloads { get; init; }
    public required bool HasPassword { get; init; }
    public required int SelectedFilesCount { get; init; }
    public required int SelectedFoldersCount { get; init; }
}
