namespace PlikShare.Mcp.Files.Get.Contracts;

public class GetFileResponseDto
{
    public required string ExternalId { get; init; }
    public required string Name { get; init; }
    public required string Extension { get; init; }
    public required string ContentType { get; init; }
    public required long SizeInBytes { get; init; }
    public required DateTime? CreatedAt { get; init; }
    public required List<FilePathItemDto> Path { get; init; }
}

public class FilePathItemDto
{
    public required string ExternalId { get; init; }
    public required string Name { get; init; }
}
