using PlikShare.Agents.Operations;
using PlikShare.Core.Utils;
using PlikShare.Mcp.Search.Contracts;

namespace PlikShare.Mcp.Search;

/// <summary>
/// Resolves a search operation's stored filters into the key human-readable ones (name terms, item
/// types, file extensions) so a human reviewing the approval sees what the agent wants to search for.
/// </summary>
public class SearchOperationDetailsResolver
{
    public SearchOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<SearchParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        return new SearchOperationDetails
        {
            NameContains = Clean(parameters.NameContains),
            Types = Clean(parameters.Types),
            Extensions = Clean(parameters.Extensions)
        };
    }

    private static List<string> Clean(string[]? values) =>
        values is null
            ? []
            : values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .ToList();
}
