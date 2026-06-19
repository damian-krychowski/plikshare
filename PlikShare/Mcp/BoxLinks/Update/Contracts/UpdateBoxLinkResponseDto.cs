namespace PlikShare.Mcp.BoxLinks.Update.Contracts;

public class UpdateBoxLinkResponseDto
{
    public required string BoxLinkExternalId { get; init; }
    public required bool UpdatedName { get; init; }
    public required bool UpdatedIsEnabled { get; init; }
    public required bool UpdatedPermissions { get; init; }
    public required bool UpdatedWidgetOrigins { get; init; }
}
