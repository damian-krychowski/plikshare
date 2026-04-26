namespace PlikShare.AuditLog.Contracts;

public record class GetAuditLogEntryDetailsResponseDto
{
    public required string ExternalId { get; init; }
    public required string CreatedAt { get; init; }
    public string? CorrelationId { get; init; }
    public required string ActorIdentityType { get; init; }
    public required string ActorIdentity { get; init; }
    public string? ActorEmail { get; init; }
    public string? ActorIp { get; init; }
    public required string EventCategory { get; init; }
    public required string EventType { get; init; }
    public required string EventSeverity { get; init; }
    public string? WorkspaceExternalId { get; init; }
    public string? BoxExternalId { get; init; }
    public string? BoxLinkExternalId { get; init; }
    public string? Details { get; init; }
}
