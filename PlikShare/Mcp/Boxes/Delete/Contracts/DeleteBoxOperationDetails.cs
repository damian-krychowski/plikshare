using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.Boxes.Delete.Contracts;

/// <summary>
/// Surfaces, for a human reviewing the approval, which box would be deleted.
/// </summary>
public class DeleteBoxOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.DeleteBox;

    public required string WorkspaceExternalId { get; init; }
    public required string BoxExternalId { get; init; }
    public required string? BoxName { get; init; }
}
