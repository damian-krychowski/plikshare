using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.BoxLinks.RegenerateAccessCode.Contracts;

/// <summary>
/// Surfaces, for a human reviewing the approval, which box link's access code would be regenerated —
/// invalidating its current URL.
/// </summary>
public class RegenerateBoxLinkAccessCodeOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.RegenerateBoxLinkAccessCode;

    public required string WorkspaceExternalId { get; init; }
    public required string BoxLinkExternalId { get; init; }
    public required string? BoxLinkName { get; init; }
}
