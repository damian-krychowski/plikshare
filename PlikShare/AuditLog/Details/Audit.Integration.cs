using PlikShare.Core.Utils;

namespace PlikShare.AuditLog.Details;

public static partial class Audit
{
    public static class Integration
    {
        public class Created
        {
            public required IntegrationRef Integration { get; init; }
        }

        public class Deleted
        {
            public required IntegrationRef Integration { get; init; }
        }

        public class NameUpdated
        {
            public required IntegrationRef Integration { get; init; }
        }

        public class ActivationChanged
        {
            public required IntegrationRef Integration { get; init; }
        }

        public static AuditLogEntry CreatedEntry(
            AuditLogActorContext actor,
            IntegrationRef integration) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Integration,
            EventType = AuditLogEventTypes.Integration.Created,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new Created {
                Integration = integration })
        };

        public static AuditLogEntry DeletedEntry(
            AuditLogActorContext actor,
            IntegrationRef integration) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Integration,
            EventType = AuditLogEventTypes.Integration.Deleted,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new Deleted {
                Integration = integration })
        };

        public static AuditLogEntry NameUpdatedEntry(
            AuditLogActorContext actor,
            IntegrationRef integration) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Integration,
            EventType = AuditLogEventTypes.Integration.NameUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new NameUpdated {
                Integration = integration })
        };

        public static AuditLogEntry ActivatedEntry(
            AuditLogActorContext actor,
            IntegrationRef integration) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Integration,
            EventType = AuditLogEventTypes.Integration.Activated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new ActivationChanged {
                Integration = integration })
        };

        public static AuditLogEntry DeactivatedEntry(
            AuditLogActorContext actor,
            IntegrationRef integration) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Integration,
            EventType = AuditLogEventTypes.Integration.Deactivated,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new ActivationChanged {
                Integration = integration })
        };
    }
}
