using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.Boxes.Members.Invite.Contracts;

/// <summary>
/// Surfaces, for a human reviewing the approval, exactly who would be invited to which box. Invitees start
/// with list-only permissions; they are widened separately via update_box_member_permissions.
/// </summary>
public class InviteBoxMembersOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.InviteBoxMembers;

    public required string WorkspaceExternalId { get; init; }
    public required string BoxExternalId { get; init; }
    public required string? BoxName { get; init; }
    public required List<string> MemberEmails { get; init; }
}
