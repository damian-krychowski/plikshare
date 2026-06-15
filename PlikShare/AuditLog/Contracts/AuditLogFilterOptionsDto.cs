namespace PlikShare.AuditLog.Contracts;

public class AuditLogFilterOptionsDto
{
    public required List<string> EventTypes { get; init; }
    public required List<string> Actors { get; init; }
    public required List<AuditLogAgentActorDto> Agents { get; init; }
}

public class AuditLogAgentActorDto
{
    public required string ExternalId { get; init; }
    public required string Name { get; init; }
}
