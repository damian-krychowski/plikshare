using PlikShare.Core.Utils;

namespace PlikShare.AuditLog.Details;

public static partial class Audit
{
    public static class AuthProvider
    {
        public class Created
        {
            public required AuthProviderRef AuthProvider { get; init; }
        }

        public class Deleted
        {
            public required AuthProviderRef AuthProvider { get; init; }
        }

        public class NameUpdated
        {
            public required AuthProviderRef AuthProvider { get; init; }
        }

        public class Updated
        {
            public required AuthProviderRef AuthProvider { get; init; }
        }

        public class ActivationChanged
        {
            public required AuthProviderRef AuthProvider { get; init; }
        }

        public class PasswordLoginToggled
        {
            public required bool IsEnabled { get; init; }
        }

        public static AuditLogEntry CreatedEntry(
            AuditLogActorContext actor,
            AuthProviderRef authProvider) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.AuthProvider,
            EventType = AuditLogEventTypes.AuthProvider.Created,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new Created {
                AuthProvider = authProvider })
        };

        public static AuditLogEntry DeletedEntry(
            AuditLogActorContext actor,
            AuthProviderRef authProvider) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.AuthProvider,
            EventType = AuditLogEventTypes.AuthProvider.Deleted,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new Deleted {
                AuthProvider = authProvider })
        };

        public static AuditLogEntry NameUpdatedEntry(
            AuditLogActorContext actor,
            AuthProviderRef authProvider) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.AuthProvider,
            EventType = AuditLogEventTypes.AuthProvider.NameUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new NameUpdated {
                AuthProvider = authProvider })
        };

        public static AuditLogEntry UpdatedEntry(
            AuditLogActorContext actor,
            AuthProviderRef authProvider) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.AuthProvider,
            EventType = AuditLogEventTypes.AuthProvider.Updated,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new Updated {
                AuthProvider = authProvider })
        };

        public static AuditLogEntry ActivatedEntry(
            AuditLogActorContext actor,
            AuthProviderRef authProvider) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.AuthProvider,
            EventType = AuditLogEventTypes.AuthProvider.Activated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new ActivationChanged {
                AuthProvider = authProvider })
        };

        public static AuditLogEntry DeactivatedEntry(
            AuditLogActorContext actor,
            AuthProviderRef authProvider) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.AuthProvider,
            EventType = AuditLogEventTypes.AuthProvider.Deactivated,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new ActivationChanged {
                AuthProvider = authProvider })
        };

        public static AuditLogEntry PasswordLoginToggledEntry(
            AuditLogActorContext actor,
            bool isEnabled) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.AuthProvider,
            EventType = AuditLogEventTypes.AuthProvider.PasswordLoginToggled,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new PasswordLoginToggled {
                IsEnabled = isEnabled })
        };
    }
}
