using PlikShare.Agents.Id;

namespace PlikShare.Agents.Create.Contracts;

public class CreateAgentResponseDto
{
    public required AgentExtId ExternalId { get; init; }
    public required string Token { get; init; }
    public required string TokenMasked { get; init; }
}
