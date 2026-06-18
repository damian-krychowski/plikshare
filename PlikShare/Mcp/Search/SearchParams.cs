namespace PlikShare.Mcp.Search;

public class SearchParams
{
    public required string[]? WorkspaceIds { get; init; }
    public required string[]? FolderIds { get; init; }
    public required string[]? ExcludeWorkspaceIds { get; init; }
    public required string[]? ExcludeFolderIds { get; init; }
    public required string[]? Types { get; init; }
    public required string[]? NameContains { get; init; }
    public required string[]? Extensions { get; init; }
    public required string[]? ContentTypes { get; init; }
    public required string? CreatedAfter { get; init; }
    public required string? CreatedBefore { get; init; }
    public required long? SizeMin { get; init; }
    public required long? SizeMax { get; init; }
    public required string? Cursor { get; init; }
    public required int? Limit { get; init; }
}
