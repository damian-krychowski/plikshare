namespace PlikShare.Agents.Tools.Contracts;

public class GetAgentToolsResponseDto
{
    public required List<AgentToolConfigDto> Tools { get; init; }
}

public class AgentToolConfigDto
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Scope { get; init; }
    public required bool IsEnabled { get; init; }
    public required bool RequiresApproval { get; init; }
    public required bool IsDefault { get; init; }
}
