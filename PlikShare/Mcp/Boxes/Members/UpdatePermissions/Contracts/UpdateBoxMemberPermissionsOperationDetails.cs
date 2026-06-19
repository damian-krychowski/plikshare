using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.Boxes.Members.UpdatePermissions.Contracts;

/// <summary>
/// Surfaces, for a human reviewing the approval, which box member's permissions would change.
/// </summary>
public class UpdateBoxMemberPermissionsOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.UpdateBoxMemberPermissions;

    public required string WorkspaceExternalId { get; init; }
    public required string BoxExternalId { get; init; }
    public required string? BoxName { get; init; }
    public required string MemberExternalId { get; init; }
    public required string? MemberEmail { get; init; }
}
