namespace PlikShare.Mcp.ShareLinks.Create;

public class CreateShareLinkParams
{
    public required string WorkspaceExternalId { get; init; }
    public required string Name { get; init; }
    public required string[] FileExternalIds { get; init; }
    public required string[] FolderExternalIds { get; init; }
    public required string[] ExcludedFileExternalIds { get; init; }
    public required string[] ExcludedFolderExternalIds { get; init; }
    public required DateTimeOffset? ExpiresAt { get; init; }
    public required int? MaxDownloads { get; init; }
    public required string? PasswordHashBase64 { get; init; }
    public required byte[]? PasswordSalt { get; init; }
}
