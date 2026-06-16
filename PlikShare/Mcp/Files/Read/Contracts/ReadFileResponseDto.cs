namespace PlikShare.Mcp.Files.Read.Contracts;

public class ReadFileResponseDto
{
    public required string Content { get; init; }
    public required long TotalSizeInBytes { get; init; }
    public required long NextOffset { get; init; }
    public required bool HasMore { get; init; }
}
