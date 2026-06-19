using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.Boxes.Members.Revoke.Contracts;

/// <summary>
/// Surfaces, for a human reviewing the approval, which member would be removed from which box.
/// </summary>
public class RevokeBoxMemberOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.RevokeBoxMember;

    public required string WorkspaceExternalId { get; init; }
    public required string BoxExternalId { get; init; }
    public required string? BoxName { get; init; }
    public required string MemberExternalId { get; init; }
    public required string? MemberEmail { get; init; }
}
