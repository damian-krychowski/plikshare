namespace PlikShare.Mcp.Files.Read;

public class ReadFileParams
{
    public required string FileExternalId { get; init; }
    public required long Offset { get; init; }
    public required int? MaxBytes { get; init; }
}
