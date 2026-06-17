namespace PlikShare.Agents.Tools.Contracts;

public class UpdateAgentToolConfigRequestDto
{
    public required bool IsEnabled { get; init; }
    public required bool RequiresApproval { get; init; }
}
