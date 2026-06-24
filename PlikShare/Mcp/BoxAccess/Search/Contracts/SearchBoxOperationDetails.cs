using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.BoxAccess.Search.Contracts;

/// <summary>
/// search_box searches for files inside a box; its details carry the box (id and name), the phrase and
/// the optional folder (its id and name) so a human reviewing the approval sees what would be searched.
/// </summary>
public class SearchBoxOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.SearchBox;

    public required string BoxExternalId { get; init; }
    public required string? BoxName { get; init; }
    public required string Phrase { get; init; }
    public required string? FolderExternalId { get; init; }
    public required string? FolderName { get; init; }
}
