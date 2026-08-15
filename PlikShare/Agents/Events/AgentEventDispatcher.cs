using PlikShare.AuditLog;
using PlikShare.Core.Clock;

namespace PlikShare.Agents.Events;

/// <summary>
/// The single fan-out point from the audit funnel to agent delivery. EVERY audit entry passes through
/// here and becomes a lean notification on the in-memory channel — no event type is ever filtered out.
/// Whether an agent actually receives an event is decided downstream, purely by access/ownership and
/// the agent's own subscriptions, never by a hardcoded notion of which events are "relevant".
/// Cheap and non-blocking on the request hot path; the projector does the real work off-thread.
/// </summary>
public class AgentEventDispatcher(
    AgentEventChannel channel,
    IClock clock)
{
    public void Publish(AuditLogEntry entry)
    {
        channel.TryPublish(new AgentEventNotification
        {
            EventType = entry.EventType,
            EventCategory = entry.EventCategory,
            WorkspaceExternalId = entry.WorkspaceExternalId,
            BoxExternalId = entry.BoxExternalId,
            ActorIdentityType = entry.Actor.IdentityType,
            ActorIdentity = entry.Actor.Identity,
            OccurredAt = clock.UtcNow
        });
    }
}
