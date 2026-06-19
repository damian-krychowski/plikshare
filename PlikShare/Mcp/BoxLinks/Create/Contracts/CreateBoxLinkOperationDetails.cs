using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.BoxLinks.Create.Contracts;

/// <summary>
/// Surfaces, for a human reviewing the approval, the name and target box of the public link that would be
/// created.
/// </summary>
public class CreateBoxLinkOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.CreateBoxLink;

    public required string WorkspaceExternalId { get; init; }
    public required string BoxExternalId { get; init; }
    public required string? BoxName { get; init; }
    public required string Name { get; init; }
}
