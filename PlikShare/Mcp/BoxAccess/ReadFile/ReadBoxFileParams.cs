namespace PlikShare.Mcp.BoxAccess.ReadFile;

public class ReadBoxFileParams
{
    public required string BoxExternalId { get; init; }
    public required string FileExternalId { get; init; }
    public required long Offset { get; init; }
    public required int? MaxBytes { get; init; }
}
