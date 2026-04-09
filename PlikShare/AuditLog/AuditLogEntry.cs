using PlikShare.Core.UserIdentity;

namespace PlikShare.AuditLog;

public record AuditLogEntry
{
    public required IUserIdentity Actor { get; init; }
    public string? ActorEmail { get; init; }
    public string? ActorIp { get; init; }
    public Guid? CorrelationId { get; init; }

    public required string EventCategory { get; init; }
    public required string EventType { get; init; }
    public required string Severity { get; init; }

    public string? WorkspaceExternalId { get; init; }
    public string? BoxExternalId { get; init; }
    public string? BoxLinkExternalId { get; init; }

    public string? DetailsJson { get; init; }
}
