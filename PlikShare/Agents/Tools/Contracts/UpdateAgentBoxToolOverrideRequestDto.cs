namespace PlikShare.Agents.Tools.Contracts;

public class UpdateAgentBoxToolOverrideRequestDto
{
    public required bool? IsEnabled { get; init; }
    public required bool? RequiresApproval { get; init; }
}
