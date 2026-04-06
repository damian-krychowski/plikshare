using System.Text.Json;
using PlikShare.Core.UserIdentity;
using PlikShare.Users.PermissionsAndRoles;

namespace PlikShare.AuditLog;

public static class Audit
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string Serialize<T>(T details) => JsonSerializer.Serialize(details, JsonOptions);

    public static class Auth
    {
        public static AuditLogEntry SignedUp(AuditLogActorContext actor, string email) => new()
        {
            Actor = actor.Identity, ActorEmail = email, ActorIp = actor.Ip, CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth, EventType = AuditLogEventTypes.Auth.SignedUp,
            Severity = AuditLogSeverities.Info
        };

        public static AuditLogEntry SignUpFailed(string? actorIp, Guid correlationId, string attemptedEmail, string reason) => new()
        {
            Actor = AnonymousIdentity.Instance, ActorEmail = attemptedEmail, ActorIp = actorIp, CorrelationId = correlationId,
            EventCategory = AuditLogEventCategories.Auth, EventType = AuditLogEventTypes.Auth.SignUpFailed,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Serialize(new AuditLogDetails.Auth.Failed { Reason = reason })
        };

        public static AuditLogEntry EmailConfirmed(AuditLogActorContext actor, string email) => new()
        {
            Actor = actor.Identity, ActorEmail = email, ActorIp = actor.Ip, CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth, EventType = AuditLogEventTypes.Auth.EmailConfirmed,
            Severity = AuditLogSeverities.Info
        };

        public static AuditLogEntry EmailConfirmationFailed(string? actorIp, Guid correlationId, string? email, string reason) => new()
        {
            Actor = AnonymousIdentity.Instance, ActorEmail = email, ActorIp = actorIp, CorrelationId = correlationId,
            EventCategory = AuditLogEventCategories.Auth, EventType = AuditLogEventTypes.Auth.EmailConfirmationFailed,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Serialize(new AuditLogDetails.Auth.Failed { Reason = reason })
        };

        public static AuditLogEntry SignedIn(AuditLogActorContext actor, string email, string method) => new()
        {
            Actor = actor.Identity, ActorEmail = email, ActorIp = actor.Ip, CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth, EventType = AuditLogEventTypes.Auth.SignedIn,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Serialize(new AuditLogDetails.Auth.SignedIn { Method = method })
        };

        public static AuditLogEntry SignInFailed(string? actorIp, Guid correlationId, string attemptedEmail, string reason) => new()
        {
            Actor = AnonymousIdentity.Instance, ActorEmail = attemptedEmail, ActorIp = actorIp, CorrelationId = correlationId,
            EventCategory = AuditLogEventCategories.Auth, EventType = AuditLogEventTypes.Auth.SignInFailed,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Serialize(new AuditLogDetails.Auth.Failed { Reason = reason })
        };

        public static AuditLogEntry SignedIn2Fa(AuditLogActorContext actor, string? email) => new()
        {
            Actor = actor.Identity, ActorEmail = email, ActorIp = actor.Ip, CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth, EventType = AuditLogEventTypes.Auth.SignedIn2Fa,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Serialize(new AuditLogDetails.Auth.SignedIn { Method = AuditLogSignInMethods.Authenticator })
        };

        public static AuditLogEntry SignedInRecoveryCode(AuditLogActorContext actor, string? email) => new()
        {
            Actor = actor.Identity, ActorEmail = email, ActorIp = actor.Ip, CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth, EventType = AuditLogEventTypes.Auth.SignedIn2Fa,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Serialize(new AuditLogDetails.Auth.SignedIn { Method = AuditLogSignInMethods.RecoveryCode })
        };

        public static AuditLogEntry SignIn2FaFailed(string? actorIp, Guid correlationId, string reason, string? email) => new()
        {
            Actor = AnonymousIdentity.Instance, ActorEmail = email, ActorIp = actorIp, CorrelationId = correlationId,
            EventCategory = AuditLogEventCategories.Auth, EventType = AuditLogEventTypes.Auth.SignIn2FaFailed,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Serialize(new AuditLogDetails.Auth.Failed { Reason = reason })
        };

        public static AuditLogEntry SignedOut(AuditLogActorContext actor) => new()
        {
            Actor = actor.Identity, ActorEmail = actor.Email, ActorIp = actor.Ip, CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth, EventType = AuditLogEventTypes.Auth.SignedOut,
            Severity = AuditLogSeverities.Info
        };

        public static AuditLogEntry PasswordChanged(AuditLogActorContext actor) => new()
        {
            Actor = actor.Identity, ActorEmail = actor.Email, ActorIp = actor.Ip, CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth, EventType = AuditLogEventTypes.Auth.PasswordChanged,
            Severity = AuditLogSeverities.Info
        };

        public static AuditLogEntry PasswordChangeFailed(AuditLogActorContext actor, string reason) => new()
        {
            Actor = actor.Identity, ActorEmail = actor.Email, ActorIp = actor.Ip, CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth, EventType = AuditLogEventTypes.Auth.PasswordChangeFailed,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Serialize(new AuditLogDetails.Auth.Failed { Reason = reason })
        };

        public static AuditLogEntry PasswordResetRequested(string? actorIp, Guid correlationId, string email) => new()
        {
            Actor = AnonymousIdentity.Instance, ActorEmail = email, ActorIp = actorIp, CorrelationId = correlationId,
            EventCategory = AuditLogEventCategories.Auth, EventType = AuditLogEventTypes.Auth.PasswordResetRequested,
            Severity = AuditLogSeverities.Info
        };

        public static AuditLogEntry PasswordResetCompleted(string? actorIp, Guid correlationId, string? email) => new()
        {
            Actor = AnonymousIdentity.Instance, ActorEmail = email, ActorIp = actorIp, CorrelationId = correlationId,
            EventCategory = AuditLogEventCategories.Auth, EventType = AuditLogEventTypes.Auth.PasswordResetCompleted,
            Severity = AuditLogSeverities.Info
        };

        public static AuditLogEntry PasswordResetFailed(string? actorIp, Guid correlationId, string? email, string reason) => new()
        {
            Actor = AnonymousIdentity.Instance, ActorEmail = email, ActorIp = actorIp, CorrelationId = correlationId,
            EventCategory = AuditLogEventCategories.Auth, EventType = AuditLogEventTypes.Auth.PasswordResetFailed,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Serialize(new AuditLogDetails.Auth.Failed { Reason = reason })
        };

        public static AuditLogEntry TwoFaEnabled(AuditLogActorContext actor) => new()
        {
            Actor = actor.Identity, ActorEmail = actor.Email, ActorIp = actor.Ip, CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth, EventType = AuditLogEventTypes.Auth.TwoFaEnabled,
            Severity = AuditLogSeverities.Info
        };

        public static AuditLogEntry TwoFaEnableFailed(AuditLogActorContext actor, string reason) => new()
        {
            Actor = actor.Identity, ActorEmail = actor.Email, ActorIp = actor.Ip, CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth, EventType = AuditLogEventTypes.Auth.TwoFaEnableFailed,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Serialize(new AuditLogDetails.Auth.Failed { Reason = reason })
        };

        public static AuditLogEntry TwoFaDisabled(AuditLogActorContext actor) => new()
        {
            Actor = actor.Identity, ActorEmail = actor.Email, ActorIp = actor.Ip, CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth, EventType = AuditLogEventTypes.Auth.TwoFaDisabled,
            Severity = AuditLogSeverities.Critical
        };

        public static AuditLogEntry RecoveryCodesRegenerated(AuditLogActorContext actor) => new()
        {
            Actor = actor.Identity, ActorEmail = actor.Email, ActorIp = actor.Ip, CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth, EventType = AuditLogEventTypes.Auth.RecoveryCodesRegenerated,
            Severity = AuditLogSeverities.Warning
        };

        public static AuditLogEntry SsoLogin(AuditLogActorContext actor, string email, string providerName) => new()
        {
            Actor = actor.Identity, ActorEmail = email, ActorIp = actor.Ip, CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth, EventType = AuditLogEventTypes.Auth.SsoLogin,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Serialize(new AuditLogDetails.Auth.Sso { ProviderName = providerName })
        };

        public static AuditLogEntry SsoUserCreated(AuditLogActorContext actor, string email, string providerName) => new()
        {
            Actor = actor.Identity, ActorEmail = email, ActorIp = actor.Ip, CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth, EventType = AuditLogEventTypes.Auth.SsoUserCreated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Serialize(new AuditLogDetails.Auth.Sso { ProviderName = providerName })
        };
    }

    public static class User
    {
        public static AuditLogEntry Invited(AuditLogActorContext actor, List<string> emails) => new()
        {
            Actor = actor.Identity, ActorEmail = actor.Email, ActorIp = actor.Ip, CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.User, EventType = AuditLogEventTypes.User.Invited,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Serialize(new AuditLogDetails.User.Invited { Emails = emails })
        };

        public static AuditLogEntry Deleted(AuditLogActorContext actor, string targetEmail) => new()
        {
            Actor = actor.Identity, ActorEmail = actor.Email, ActorIp = actor.Ip, CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.User, EventType = AuditLogEventTypes.User.Deleted,
            Severity = AuditLogSeverities.Critical,
            DetailsJson = Serialize(new AuditLogDetails.User.Deleted { TargetEmail = targetEmail })
        };

        public static AuditLogEntry PermissionsAndRolesUpdated(AuditLogActorContext actor, string targetEmail, UserPermissionsAndRolesDto request) => new()
        {
            Actor = actor.Identity, ActorEmail = actor.Email, ActorIp = actor.Ip, CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.User, EventType = AuditLogEventTypes.User.PermissionsAndRolesUpdated,
            Severity = AuditLogSeverities.Critical,
            DetailsJson = Serialize(new AuditLogDetails.User.PermissionsAndRolesUpdated
            {
                TargetEmail = targetEmail,
                IsAdmin = request.IsAdmin,
                Permissions = request.GetPermissionsList()
            })
        };

        public static AuditLogEntry MaxWorkspaceNumberUpdated(AuditLogActorContext actor, int? value) => new()
        {
            Actor = actor.Identity, ActorEmail = actor.Email, ActorIp = actor.Ip, CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.User, EventType = AuditLogEventTypes.User.MaxWorkspaceNumberUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Serialize(new AuditLogDetails.User.LimitUpdated { Value = value })
        };

        public static AuditLogEntry DefaultMaxWorkspaceSizeUpdated(AuditLogActorContext actor, long? value) => new()
        {
            Actor = actor.Identity, ActorEmail = actor.Email, ActorIp = actor.Ip, CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.User, EventType = AuditLogEventTypes.User.DefaultMaxWorkspaceSizeUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Serialize(new AuditLogDetails.User.LimitUpdated { Value = value })
        };

        public static AuditLogEntry DefaultMaxWorkspaceTeamMembersUpdated(AuditLogActorContext actor, int? value) => new()
        {
            Actor = actor.Identity, ActorEmail = actor.Email, ActorIp = actor.Ip, CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.User, EventType = AuditLogEventTypes.User.DefaultMaxWorkspaceTeamMembersUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Serialize(new AuditLogDetails.User.LimitUpdated { Value = value })
        };
    }
}
