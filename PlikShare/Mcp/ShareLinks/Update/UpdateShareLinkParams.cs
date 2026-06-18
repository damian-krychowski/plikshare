namespace PlikShare.Mcp.ShareLinks.Update;

public class UpdateShareLinkParams
{
    public required string WorkspaceExternalId { get; init; }
    public required string ShareLinkExternalId { get; init; }
    public required bool UpdateName { get; init; }
    public required string? Name { get; init; }
    public required bool UpdateExpiration { get; init; }
    public required DateTimeOffset? ExpiresAt { get; init; }
    public required bool UpdateMaxDownloads { get; init; }
    public required int? MaxDownloads { get; init; }
    public required bool UpdatePassword { get; init; }
    public required string? PasswordHashBase64 { get; init; }
    public required byte[]? PasswordSalt { get; init; }
    public required bool PasswordSet { get; init; }
}
