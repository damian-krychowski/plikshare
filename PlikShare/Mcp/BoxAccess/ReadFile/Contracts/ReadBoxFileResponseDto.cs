namespace PlikShare.Mcp.BoxAccess.ReadFile.Contracts;

public class ReadBoxFileResponseDto
{
    public required string Content { get; init; }
    public required long TotalSizeInBytes { get; init; }
    public required long NextOffset { get; init; }
    public required bool HasMore { get; init; }
}
