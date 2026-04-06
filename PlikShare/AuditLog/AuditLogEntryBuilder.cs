using System.Text.Json;
using PlikShare.Core.UserIdentity;

namespace PlikShare.AuditLog;

public static class AuditLogEntryBuilder
{
    public static AuditLogEntry AuthSignedUp(
        AuditLogActorContext actor,
        string email)
    {
        return new AuditLogEntry
        {
            Actor = actor.Identity,
            ActorEmail = email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.SignedUp,
            Severity = AuditLogSeverities.Info
        };
    }

    public static AuditLogEntry AuthEmailConfirmed(
        AuditLogActorContext actor,
        string email)
    {
        return new AuditLogEntry
        {
            Actor = actor.Identity,
            ActorEmail = email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.EmailConfirmed,
            Severity = AuditLogSeverities.Info
        };
    }

    public static AuditLogEntry AuthSignedIn(
        AuditLogActorContext actor,
        string email,
        string method)
    {
        return new AuditLogEntry
        {
            Actor = actor.Identity,
            ActorEmail = email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.SignedIn,
            Severity = AuditLogSeverities.Info,
            DetailsJson = JsonSerializer.Serialize(new { method })
        };
    }

    public static AuditLogEntry AuthSignInFailed(
        string? actorIp,
        Guid correlationId,
        string attemptedEmail,
        string reason)
    {
        return new AuditLogEntry
        {
            Actor = new GenericUserIdentity("anonymous", "anonymous"),
            ActorEmail = attemptedEmail,
            ActorIp = actorIp,
            CorrelationId = correlationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.SignInFailed,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = JsonSerializer.Serialize(new { reason })
        };
    }

    public static AuditLogEntry AuthSignedIn2Fa(
        AuditLogActorContext actor,
        string? email)
    {
        return new AuditLogEntry
        {
            Actor = actor.Identity,
            ActorEmail = email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.SignedIn2Fa,
            Severity = AuditLogSeverities.Info,
            DetailsJson = JsonSerializer.Serialize(new { method = "authenticator" })
        };
    }

    public static AuditLogEntry AuthSignedInRecoveryCode(
        AuditLogActorContext actor,
        string? email)
    {
        return new AuditLogEntry
        {
            Actor = actor.Identity,
            ActorEmail = email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.SignedIn2Fa,
            Severity = AuditLogSeverities.Info,
            DetailsJson = JsonSerializer.Serialize(new { method = "recovery-code" })
        };
    }

    public static AuditLogEntry AuthSignedOut(
        AuditLogActorContext actor)
    {
        return new AuditLogEntry
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.SignedOut,
            Severity = AuditLogSeverities.Info
        };
    }

    public static AuditLogEntry AuthPasswordChanged(
        AuditLogActorContext actor)
    {
        return new AuditLogEntry
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.PasswordChanged,
            Severity = AuditLogSeverities.Info
        };
    }

    public static AuditLogEntry AuthPasswordResetRequested(
        string? actorIp,
        Guid correlationId,
        string email)
    {
        return new AuditLogEntry
        {
            Actor = new GenericUserIdentity("anonymous", "anonymous"),
            ActorEmail = email,
            ActorIp = actorIp,
            CorrelationId = correlationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.PasswordResetRequested,
            Severity = AuditLogSeverities.Info
        };
    }

    public static AuditLogEntry AuthPasswordResetCompleted(
        string? actorIp,
        Guid correlationId,
        string userExternalId)
    {
        return new AuditLogEntry
        {
            Actor = new GenericUserIdentity("anonymous", "anonymous"),
            ActorIp = actorIp,
            CorrelationId = correlationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.PasswordResetCompleted,
            Severity = AuditLogSeverities.Info,
            DetailsJson = JsonSerializer.Serialize(new { userExternalId })
        };
    }

    public static AuditLogEntry Auth2FaEnabled(
        AuditLogActorContext actor)
    {
        return new AuditLogEntry
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.TwoFaEnabled,
            Severity = AuditLogSeverities.Info
        };
    }

    public static AuditLogEntry Auth2FaDisabled(
        AuditLogActorContext actor)
    {
        return new AuditLogEntry
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.TwoFaDisabled,
            Severity = AuditLogSeverities.Critical
        };
    }

    public static AuditLogEntry AuthRecoveryCodesRegenerated(
        AuditLogActorContext actor)
    {
        return new AuditLogEntry
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.RecoveryCodesRegenerated,
            Severity = AuditLogSeverities.Warning
        };
    }

    public static AuditLogEntry AuthSignUpFailed(
        string? actorIp,
        Guid correlationId,
        string attemptedEmail,
        string reason)
    {
        return new AuditLogEntry
        {
            Actor = new GenericUserIdentity("anonymous", "anonymous"),
            ActorEmail = attemptedEmail,
            ActorIp = actorIp,
            CorrelationId = correlationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.SignUpFailed,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = JsonSerializer.Serialize(new { reason })
        };
    }

    public static AuditLogEntry AuthEmailConfirmationFailed(
        string? actorIp,
        Guid correlationId,
        string userExternalId,
        string reason)
    {
        return new AuditLogEntry
        {
            Actor = new GenericUserIdentity("anonymous", "anonymous"),
            ActorIp = actorIp,
            CorrelationId = correlationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.EmailConfirmationFailed,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = JsonSerializer.Serialize(new { reason, userExternalId })
        };
    }

    public static AuditLogEntry Auth2FaFailed(
        string? actorIp,
        Guid correlationId,
        string reason,
        string? email)
    {
        return new AuditLogEntry
        {
            Actor = new GenericUserIdentity("anonymous", "anonymous"),
            ActorEmail = email,
            ActorIp = actorIp,
            CorrelationId = correlationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.SignIn2FaFailed,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = JsonSerializer.Serialize(new { reason })
        };
    }

    public static AuditLogEntry AuthPasswordChangeFailed(
        AuditLogActorContext actor,
        string reason)
    {
        return new AuditLogEntry
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.PasswordChangeFailed,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = JsonSerializer.Serialize(new { reason })
        };
    }

    public static AuditLogEntry AuthPasswordResetFailed(
        string? actorIp,
        Guid correlationId,
        string userExternalId,
        string reason)
    {
        return new AuditLogEntry
        {
            Actor = new GenericUserIdentity("anonymous", "anonymous"),
            ActorIp = actorIp,
            CorrelationId = correlationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.PasswordResetFailed,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = JsonSerializer.Serialize(new { reason, userExternalId })
        };
    }

    public static AuditLogEntry Auth2FaEnableFailed(
        AuditLogActorContext actor,
        string reason)
    {
        return new AuditLogEntry
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.TwoFaEnableFailed,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = JsonSerializer.Serialize(new { reason })
        };
    }

    public static AuditLogEntry AuthSsoLogin(
        AuditLogActorContext actor,
        string email,
        string providerName)
    {
        return new AuditLogEntry
        {
            Actor = actor.Identity,
            ActorEmail = email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.SsoLogin,
            Severity = AuditLogSeverities.Info,
            DetailsJson = JsonSerializer.Serialize(new { providerName })
        };
    }

    public static AuditLogEntry AuthSsoUserCreated(
        AuditLogActorContext actor,
        string email,
        string providerName)
    {
        return new AuditLogEntry
        {
            Actor = actor.Identity,
            ActorEmail = email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.SsoUserCreated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = JsonSerializer.Serialize(new { providerName, email })
        };
    }
}
