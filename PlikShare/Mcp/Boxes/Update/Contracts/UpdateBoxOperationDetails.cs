using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.Boxes.Update.Contracts;

/// <summary>
/// Surfaces, for a human reviewing the approval, which box would change and what would change about it —
/// its name, enabled state and/or the folder it exposes.
/// </summary>
public class UpdateBoxOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.UpdateBox;

    public required string WorkspaceExternalId { get; init; }
    public required string BoxExternalId { get; init; }
    public required string? CurrentName { get; init; }

    public required bool UpdateName { get; init; }
    public required string? NewName { get; init; }

    public required bool UpdateIsEnabled { get; init; }
    public required bool? IsEnabled { get; init; }

    public required bool UpdateFolder { get; init; }
    public required string? FolderExternalId { get; init; }
}
