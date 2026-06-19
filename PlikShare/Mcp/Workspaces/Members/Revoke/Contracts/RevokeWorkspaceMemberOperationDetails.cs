using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.Workspaces.Members.Revoke.Contracts;

/// <summary>
/// Surfaces, for a human reviewing the approval, which member would be removed from which workspace.
/// </summary>
public class RevokeWorkspaceMemberOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.RevokeWorkspaceMember;

    public required string WorkspaceExternalId { get; init; }
    public required string? WorkspaceName { get; init; }
    public required string MemberExternalId { get; init; }
    public required string? MemberEmail { get; init; }
}
