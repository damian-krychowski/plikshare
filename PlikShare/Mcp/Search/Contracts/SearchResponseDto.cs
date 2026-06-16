namespace PlikShare.Mcp.Search.Contracts;

public class SearchResponseDto
{
    public required List<SearchEntryDto> Entries { get; init; }
    public required string? NextCursor { get; init; }
    public required bool HasMore { get; init; }
}

public class SearchEntryDto
{
    public required string Type { get; init; }
    public required string ExternalId { get; init; }
    public required string Name { get; init; }
    public required string? Extension { get; init; }
    public required string? ContentType { get; init; }
    public required long? SizeInBytes { get; init; }
    public required DateTime? CreatedAt { get; init; }
    public required string? FolderExternalId { get; init; }
    public required string WorkspaceExternalId { get; init; }
}
