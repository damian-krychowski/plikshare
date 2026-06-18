using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.Search.Contracts;

/// <summary>
/// A search has no single target — its approval details surface the key human-readable filters so a
/// reviewer can see what the agent is looking for.
/// </summary>
public class SearchOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.Search;

    public required List<string> NameContains { get; init; }
    public required List<string> Types { get; init; }
    public required List<string> Extensions { get; init; }
}
