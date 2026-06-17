namespace PlikShare.Agents.Tools.Contracts;

public class GetAgentWorkspaceToolsResponseDto
{
    public required List<AgentWorkspaceToolConfigDto> Tools { get; init; }
}

public class AgentWorkspaceToolConfigDto
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required bool IsAvailable { get; init; }

    public required bool GlobalIsEnabled { get; init; }
    public required bool GlobalRequiresApproval { get; init; }

    public required bool? OverrideIsEnabled { get; init; }
    public required bool? OverrideRequiresApproval { get; init; }

    public required bool EffectiveIsEnabled { get; init; }
    public required bool EffectiveRequiresApproval { get; init; }
}
