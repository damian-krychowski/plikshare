namespace PlikShare.Agents.Events;

/// <summary>
/// The lean, agent-facing projection of an audit entry that flows through the in-memory channel to
/// the projector. Deliberately a signal, not a data payload: it carries the event type, scope and
/// actor — never the (potentially encrypted) audit details. The agent reacts by calling tools to
/// fetch whatever actually changed.
/// </summary>
public sealed record AgentEventNotification
{
    public required string EventType { get; init; }
    public required string EventCategory { get; init; }
    public string? WorkspaceExternalId { get; init; }
    public string? BoxExternalId { get; init; }
    public required string ActorIdentityType { get; init; }
    public required string ActorIdentity { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
}
