using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.Workspaces.Members.Invite.Contracts;

/// <summary>
/// Surfaces, for a human reviewing the approval, exactly who would be invited to which workspace and
/// whether the invitees would be allowed to share it further.
/// </summary>
public class InviteWorkspaceMembersOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.InviteWorkspaceMembers;

    public required string WorkspaceExternalId { get; init; }
    public required string? WorkspaceName { get; init; }
    public required List<string> MemberEmails { get; init; }
    public required bool AllowShare { get; init; }
}
