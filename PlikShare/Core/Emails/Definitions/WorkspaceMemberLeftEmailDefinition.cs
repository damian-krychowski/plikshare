namespace PlikShare.Core.Emails.Definitions;

public class WorkspaceMemberLeftEmailDefinition
{
    public required string WorkspaceName { get; init; }
    public required string MemberEmail { get; init; }
}