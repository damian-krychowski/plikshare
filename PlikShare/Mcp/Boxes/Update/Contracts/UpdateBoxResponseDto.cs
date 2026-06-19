namespace PlikShare.Mcp.Boxes.Update.Contracts;

public class UpdateBoxResponseDto
{
    public required string BoxExternalId { get; init; }
    public required string? Name { get; init; }
    public required bool? IsEnabled { get; init; }
    public required string? FolderExternalId { get; init; }
}
