namespace PlikShare.Workspaces.UpdateMaxTeamMembers.Contracts;

public class UpdateWorkspaceMaxTeamMembersRequestDto
{
    public required int? MaxTeamMembers { get; init; }
}