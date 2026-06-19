namespace PlikShare.Mcp.Boxes.Members.Revoke;

public class RevokeBoxMemberParams
{
    public required string WorkspaceExternalId { get; init; }
    public required string BoxExternalId { get; init; }
    public required string MemberExternalId { get; init; }
}
