namespace PlikShare.Agents.Tools.Contracts;

public class UpdateAgentWorkspaceToolOverrideRequestDto
{
    public required bool? IsEnabled { get; init; }
    public required bool? RequiresApproval { get; init; }
}
