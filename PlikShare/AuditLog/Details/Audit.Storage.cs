using PlikShare.Core.Utils;

namespace PlikShare.AuditLog.Details;

public static partial class Audit
{
    public static class Storage
    {
        public class Created
        {
            public required StorageRef Storage { get; init; }
        }

        public class Deleted
        {
            public required StorageRef Storage { get; init; }
        }

        public class NameUpdated
        {
            public required StorageRef Storage { get; init; }
        }

        public class DetailsUpdated
        {
            public required StorageRef Storage { get; init; }
        }

        public static AuditLogEntry CreatedEntry(
            AuditLogActorContext actor,
            StorageRef storage) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Storage,
            EventType = AuditLogEventTypes.Storage.Created,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new Created {
                Storage = storage })
        };

        public static AuditLogEntry DeletedEntry(
            AuditLogActorContext actor,
            StorageRef storage) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Storage,
            EventType = AuditLogEventTypes.Storage.Deleted,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new Deleted {
                Storage = storage })
        };

        public static AuditLogEntry NameUpdatedEntry(
            AuditLogActorContext actor,
            StorageRef storage) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Storage,
            EventType = AuditLogEventTypes.Storage.NameUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new NameUpdated {
                Storage = storage })
        };

        public static AuditLogEntry DetailsUpdatedEntry(
            AuditLogActorContext actor,
            StorageRef storage) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Storage,
            EventType = AuditLogEventTypes.Storage.DetailsUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new DetailsUpdated {
                Storage = storage })
        };

    }
}
