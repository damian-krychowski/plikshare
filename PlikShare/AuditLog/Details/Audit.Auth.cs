using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;

namespace PlikShare.AuditLog.Details;

public static partial class Audit
{
    public static class Auth
    {
        public class SignedIn
        {
            public required string Method { get; init; }
        }

        public class Failed
        {
            public required string Reason { get; init; }
        }

        public class Sso
        {
            public required string ProviderName { get; init; }
        }

          public static AuditLogEntry SignedUpEntry(
            AuditLogActorContext actor,
            string email) => new()
        {
            Actor = actor.Identity,
            ActorEmail = email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.SignedUp,
            Severity = AuditLogSeverities.Info
        };

        public static AuditLogEntry SignUpFailedEntry(
            string? actorIp,
            Guid correlationId,
            string attemptedEmail,
            string reason) => new()
        {
            Actor = AnonymousIdentity.Instance,
            ActorEmail = attemptedEmail,
            ActorIp = actorIp,
            CorrelationId = correlationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.SignUpFailed,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new Failed {
                Reason = reason })
        };

        public static AuditLogEntry EmailConfirmedEntry(
            AuditLogActorContext actor,
            string email) => new()
        {
            Actor = actor.Identity,
            ActorEmail = email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.EmailConfirmed,
            Severity = AuditLogSeverities.Info
        };

        public static AuditLogEntry EmailConfirmationFailedEntry(
            string? actorIp,
            Guid correlationId,
            string? email,
            string reason) => new()
        {
            Actor = AnonymousIdentity.Instance,
            ActorEmail = email,
            ActorIp = actorIp,
            CorrelationId = correlationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.EmailConfirmationFailed,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new Failed {
                Reason = reason })
        };

        public static AuditLogEntry SignedInSuccessEntry(
            AuditLogActorContext actor,
            string email,
            string method) => new()
        {
            Actor = actor.Identity,
            ActorEmail = email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.SignedIn,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new SignedIn {
                Method = method })
        };

        public static AuditLogEntry SignInFailedEntry(
            string? actorIp,
            Guid correlationId,
            string attemptedEmail,
            string reason) => new()
        {
            Actor = AnonymousIdentity.Instance,
            ActorEmail = attemptedEmail,
            ActorIp = actorIp,
            CorrelationId = correlationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.SignInFailed,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new Failed {
                Reason = reason })
        };

        public static AuditLogEntry SignedIn2FaEntry(
            AuditLogActorContext actor,
            string? email) => new()
        {
            Actor = actor.Identity,
            ActorEmail = email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.SignedIn2Fa,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new SignedIn {
                Method = AuditLogSignInMethods.Authenticator })
        };

        public static AuditLogEntry SignedInRecoveryCodeEntry(
            AuditLogActorContext actor,
            string? email) => new()
        {
            Actor = actor.Identity,
            ActorEmail = email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.SignedIn2Fa,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new SignedIn {
                Method = AuditLogSignInMethods.RecoveryCode })
        };

        public static AuditLogEntry SignIn2FaFailedEntry(
            string? actorIp,
            Guid correlationId,
            string reason,
            string? email) => new()
        {
            Actor = AnonymousIdentity.Instance,
            ActorEmail = email,
            ActorIp = actorIp,
            CorrelationId = correlationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.SignIn2FaFailed,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new Failed {
                Reason = reason })
        };

        public static AuditLogEntry SignedOutEntry(
            AuditLogActorContext actor) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.SignedOut,
            Severity = AuditLogSeverities.Info
        };

        public static AuditLogEntry PasswordChangedEntry(
            AuditLogActorContext actor) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.PasswordChanged,
            Severity = AuditLogSeverities.Info
        };

        public static AuditLogEntry PasswordChangeFailedEntry(
            AuditLogActorContext actor,
            string reason) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.PasswordChangeFailed,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new Failed {
                Reason = reason })
        };

        public static AuditLogEntry PasswordResetRequestedEntry(
            string? actorIp,
            Guid correlationId,
            string email) => new()
        {
            Actor = AnonymousIdentity.Instance,
            ActorEmail = email,
            ActorIp = actorIp,
            CorrelationId = correlationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.PasswordResetRequested,
            Severity = AuditLogSeverities.Info
        };

        public static AuditLogEntry PasswordResetCompletedEntry(
            string? actorIp,
            Guid correlationId,
            string? email) => new()
        {
            Actor = AnonymousIdentity.Instance,
            ActorEmail = email,
            ActorIp = actorIp,
            CorrelationId = correlationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.PasswordResetCompleted,
            Severity = AuditLogSeverities.Info
        };

        public static AuditLogEntry PasswordResetFailedEntry(
            string? actorIp,
            Guid correlationId,
            string? email,
            string reason) => new()
        {
            Actor = AnonymousIdentity.Instance,
            ActorEmail = email,
            ActorIp = actorIp,
            CorrelationId = correlationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.PasswordResetFailed,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new Failed {
                Reason = reason })
        };

        public static AuditLogEntry TwoFaEnabledEntry(
            AuditLogActorContext actor) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.TwoFaEnabled,
            Severity = AuditLogSeverities.Info
        };

        public static AuditLogEntry TwoFaEnableFailedEntry(
            AuditLogActorContext actor,
            string reason) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.TwoFaEnableFailed,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new Failed {
                Reason = reason })
        };

        public static AuditLogEntry TwoFaDisabledEntry(
            AuditLogActorContext actor) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.TwoFaDisabled,
            Severity = AuditLogSeverities.Critical
        };

        public static AuditLogEntry RecoveryCodesRegeneratedEntry(
            AuditLogActorContext actor) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.RecoveryCodesRegenerated,
            Severity = AuditLogSeverities.Warning
        };

        public static AuditLogEntry SsoLoginEntry(
            AuditLogActorContext actor,
            string email,
            string providerName) => new()
        {
            Actor = actor.Identity,
            ActorEmail = email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.SsoLogin,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new Sso {
                ProviderName = providerName })
        };

        public static AuditLogEntry SsoUserCreatedEntry(
            AuditLogActorContext actor,
            string email,
            string providerName) => new()
        {
            Actor = actor.Identity,
            ActorEmail = email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.SsoUserCreated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new Sso {
                ProviderName = providerName })
        };
    }
}
