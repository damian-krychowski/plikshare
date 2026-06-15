using System.Text.Json.Serialization;

namespace PlikShare.AgentSkills.Contracts;

public class AgentSkillsIndexDto
{
    [JsonPropertyName("$schema")]
    public required string Schema { get; init; }

    public required List<AgentSkillEntryDto> Skills { get; init; }
}

public class AgentSkillEntryDto
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required string Description { get; init; }
    public required string Url { get; init; }
    public required string Digest { get; init; }
}
