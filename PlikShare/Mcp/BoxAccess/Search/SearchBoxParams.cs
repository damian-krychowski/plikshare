namespace PlikShare.Mcp.BoxAccess.Search;

public class SearchBoxParams
{
    public required string BoxExternalId { get; init; }
    public required string Phrase { get; init; }
    public required string? FolderExternalId { get; init; }
}
