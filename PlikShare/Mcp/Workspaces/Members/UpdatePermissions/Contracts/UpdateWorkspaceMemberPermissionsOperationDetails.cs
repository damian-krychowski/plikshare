using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.Workspaces.Members.UpdatePermissions.Contracts;

/// <summary>
/// Surfaces, for a human reviewing the approval, which workspace member would have their permissions
/// changed and to what.
/// </summary>
public class UpdateWorkspaceMemberPermissionsOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.UpdateWorkspaceMemberPermissions;

    public required string WorkspaceExternalId { get; init; }
    public required string? WorkspaceName { get; init; }
    public required string MemberExternalId { get; init; }
    public required string? MemberEmail { get; init; }
    public required bool AllowShare { get; init; }
}
