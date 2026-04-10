using PlikShare.Core.Utils;

namespace PlikShare.AuditLog.Details;

public static partial class Audit
{
    public static class EmailProvider
    {
        public class Created
        {
            public required EmailProviderRef EmailProvider { get; init; }
            public required string EmailFrom { get; init; }
        }

        public class Deleted
        {
            public required EmailProviderRef EmailProvider { get; init; }
        }

        public class NameUpdated
        {
            public required EmailProviderRef EmailProvider { get; init; }
        }

        public class ActivationChanged
        {
            public required EmailProviderRef EmailProvider { get; init; }
        }

        public class ConfirmationEmailResent
        {
            public required EmailProviderRef EmailProvider { get; init; }
        }

        public static AuditLogEntry CreatedEntry(
            AuditLogActorContext actor,
            EmailProviderRef emailProvider,
            string emailFrom) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.EmailProvider,
            EventType = AuditLogEventTypes.EmailProvider.Created,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new Created {
                EmailProvider = emailProvider,
                EmailFrom = emailFrom })
        };

        public static AuditLogEntry DeletedEntry(
            AuditLogActorContext actor,
            EmailProviderRef emailProvider) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.EmailProvider,
            EventType = AuditLogEventTypes.EmailProvider.Deleted,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new Deleted {
                EmailProvider = emailProvider })
        };

        public static AuditLogEntry NameUpdatedEntry(
            AuditLogActorContext actor,
            EmailProviderRef emailProvider) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.EmailProvider,
            EventType = AuditLogEventTypes.EmailProvider.NameUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new NameUpdated {
                EmailProvider = emailProvider })
        };

        public static AuditLogEntry ActivatedEntry(
            AuditLogActorContext actor,
            EmailProviderRef emailProvider) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.EmailProvider,
            EventType = AuditLogEventTypes.EmailProvider.Activated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new ActivationChanged {
                EmailProvider = emailProvider })
        };

        public static AuditLogEntry DeactivatedEntry(
            AuditLogActorContext actor,
            EmailProviderRef emailProvider) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.EmailProvider,
            EventType = AuditLogEventTypes.EmailProvider.Deactivated,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new ActivationChanged {
                EmailProvider = emailProvider })
        };

        public static AuditLogEntry ConfirmedEntry(
            AuditLogActorContext actor,
            EmailProviderRef emailProvider) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.EmailProvider,
            EventType = AuditLogEventTypes.EmailProvider.Confirmed,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new ActivationChanged {
                EmailProvider = emailProvider })
        };

        public static AuditLogEntry ConfirmationEmailResentEntry(
            AuditLogActorContext actor,
            EmailProviderRef emailProvider) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.EmailProvider,
            EventType = AuditLogEventTypes.EmailProvider.ConfirmationEmailResent,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new ConfirmationEmailResent {
                EmailProvider = emailProvider })
        };
    }
}
