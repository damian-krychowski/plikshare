namespace PlikShare.Mcp.ShareLinks.Get.Contracts;

public class GetShareLinkResponseDto
{
    public required string ExternalId { get; init; }
    public required string Name { get; init; }
    public required string? Url { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset? ExpiresAt { get; init; }
    public required int? MaxDownloads { get; init; }
    public required int DownloadsCount { get; init; }
    public required bool HasPassword { get; init; }
    public required string? CreatedByAgentExternalId { get; init; }
    public required List<string> SelectedFileExternalIds { get; init; }
    public required List<string> SelectedFolderExternalIds { get; init; }
    public required List<string> ExcludedFileExternalIds { get; init; }
    public required List<string> ExcludedFolderExternalIds { get; init; }
}
