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

        public class MasterPasswordReset
        {
            public required StorageRef Storage { get; init; }
        }

        public class MasterPasswordResetFailed
        {
            public required StorageRef Storage { get; init; }
            public required string Reason { get; init; }
        }

        public class MasterPasswordChanged
        {
            public required StorageRef Storage { get; init; }
        }

        public class MasterPasswordChangeFailed
        {
            public required StorageRef Storage { get; init; }
            public required string Reason { get; init; }
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

        public static AuditLogEntry MasterPasswordResetEntry(
            AuditLogActorContext actor,
            StorageRef storage) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Storage,
            EventType = AuditLogEventTypes.Storage.MasterPasswordReset,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new MasterPasswordReset {
                Storage = storage })
        };

        public static AuditLogEntry MasterPasswordResetFailedEntry(
            AuditLogActorContext actor,
            StorageRef storage,
            string reason) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Storage,
            EventType = AuditLogEventTypes.Storage.MasterPasswordResetFailed,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new MasterPasswordResetFailed {
                Storage = storage,
                Reason = reason })
        };

        public static AuditLogEntry MasterPasswordChangedEntry(
            AuditLogActorContext actor,
            StorageRef storage) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Storage,
            EventType = AuditLogEventTypes.Storage.MasterPasswordChanged,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new MasterPasswordChanged {
                Storage = storage })
        };

        public static AuditLogEntry MasterPasswordChangeFailedEntry(
            AuditLogActorContext actor,
            StorageRef storage,
            string reason) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Storage,
            EventType = AuditLogEventTypes.Storage.MasterPasswordChangeFailed,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new MasterPasswordChangeFailed {
                Storage = storage,
                Reason = reason })
        };
    }
}
