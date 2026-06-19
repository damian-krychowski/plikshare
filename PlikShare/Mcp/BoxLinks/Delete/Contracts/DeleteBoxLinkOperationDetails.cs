using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.BoxLinks.Delete.Contracts;

/// <summary>
/// Surfaces, for a human reviewing the approval, which box link would be deleted.
/// </summary>
public class DeleteBoxLinkOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.DeleteBoxLink;

    public required string WorkspaceExternalId { get; init; }
    public required string BoxLinkExternalId { get; init; }
    public required string? BoxLinkName { get; init; }
}
