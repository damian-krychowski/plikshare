using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.BoxLinks.Update.Contracts;

/// <summary>
/// Surfaces, for a human reviewing the approval, which box link would change and what would change about
/// it — its name, enabled state, permissions and/or widget origins.
/// </summary>
public class UpdateBoxLinkOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.UpdateBoxLink;

    public required string WorkspaceExternalId { get; init; }
    public required string BoxLinkExternalId { get; init; }
    public required string? CurrentName { get; init; }

    public required bool UpdateName { get; init; }
    public required string? NewName { get; init; }

    public required bool UpdateIsEnabled { get; init; }
    public required bool? IsEnabled { get; init; }

    public required bool UpdatePermissions { get; init; }
    public required bool UpdateWidgetOrigins { get; init; }
}
