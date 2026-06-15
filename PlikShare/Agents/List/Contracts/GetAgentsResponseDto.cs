using PlikShare.Agents.Id;

namespace PlikShare.Agents.List.Contracts;

public class GetAgentsResponseDto
{
    public required List<Agent> Items { get; init; }

    public class Agent
    {
        public required AgentExtId ExternalId { get; init; }
        public required string Name { get; init; }
        public required bool IsEnabled { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
    }
}
